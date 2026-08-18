# InventoryApp — .NET MAUI Blazor Hybrid + gRPC Inventory Management

A working small-to-medium inventory management system. The MAUI Blazor Hybrid client talks to an
ASP.NET Core backend **exclusively over gRPC**; the client never touches the database. The UI is
built with **Microsoft Fluent UI Blazor**.

> **Important:** this solution was written without a .NET SDK available in the authoring
> environment, so it has **not been compiled**. Expect to fix a small number of API-surface
> details on first build (Fluent UI component parameters in particular move between minor
> versions). The architecture, contracts, business rules and data flow are complete and consistent.

---

## 1. Solution layout

```
InventoryApp/
├── InventoryApp.sln
├── global.json                     .NET 9 SDK pin
├── Directory.Build.props
└── src/
    ├── InventoryApp.Contracts/     .proto contracts + shared permission model
    ├── InventoryApp.Domain/        Entities, enums, domain invariants
    ├── InventoryApp.Application/   Business logic, validation, orchestration
    ├── InventoryApp.Infrastructure/EF Core, SQLite, JWT, password hashing, seeding
    ├── InventoryApp.Api/           ASP.NET Core gRPC host
    └── InventoryApp.Client/        .NET MAUI Blazor Hybrid app
```

Dependency direction: `Api → Infrastructure → Application → Domain`, with `Contracts` shared by
`Api`, `Application` and `Client`. Nothing points back inward.

### End-to-end flow

```
Fluent UI component
  → Blazor page
    → InventoryApiClient (typed façade)
      → gRPC-Web channel + AuthInterceptor
        → ASP.NET Core gRPC service (thin, [Authorize] per permission)
          → Application service (validation + business rules)
            → EF Core / SQLite
```

---

## 2. Two design decisions worth knowing

**The Application layer speaks in protobuf contract messages.** Rather than maintaining a second,
near-identical set of DTOs and a mapping layer between them, application services accept and
return the generated contract types. Domain entities still never leave the server — they are
projected in `Mapping/ContractMappings.cs`. This removes an entire layer of duplication. If you
later need the contract and the application model to diverge (versioned APIs, a REST façade
alongside gRPC), reintroduce application DTOs and map in the gRPC service classes, which are
deliberately one-line pass-throughs.

**Transport is gRPC-Web, not raw gRPC.** Android and iOS ship HTTP stacks without the HTTP/2
trailer support that native gRPC requires. gRPC-Web (`Grpc.Net.Client.Web` on the client,
`Grpc.AspNetCore.Web` on the server) works identically across Windows, Android, iOS and macOS
with no per-platform branching. The contracts and generated code are unchanged.

---

## 3. Prerequisites

- .NET 9 SDK
- MAUI workloads: `dotnet workload install maui`
- Windows: Visual Studio 2022 17.12+ with the *.NET Multi-platform App UI development* workload
- Android: Android SDK (API 24+) — installed with the workload
- iOS/macOS: a Mac with Xcode 15+

Add the two Open Sans font files to `src/InventoryApp.Client/Resources/Fonts/`
(see the README there), or delete the two `fonts.AddFont(...)` lines in `MauiProgram.cs`.

---

## 4. Running the backend

```bash
cd InventoryApp

# One-time: set a real signing key (do not ship the placeholder in appsettings.json)
dotnet user-secrets set "Jwt:SigningKey" "a-long-random-secret-of-at-least-32-characters" \
  --project src/InventoryApp.Api

dotnet restore
dotnet run --project src/InventoryApp.Api
```

The API listens on `https://localhost:7266` (and `http://localhost:5266`). On first start it
creates `inventory.db` and seeds sample data. Visit `https://localhost:7266/health` to confirm
it is up.

Trust the development certificate once, or the mobile clients will reject it:

```bash
dotnet dev-certs https --trust
```

### EF Core migrations

The app uses `EnsureCreated` when no migrations exist, so it runs out of the box. For a real
deployment, generate migrations and the seeder will call `Migrate()` instead automatically:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate \
  -p src/InventoryApp.Infrastructure \
  -s src/InventoryApp.Api
```

To switch database providers, change `UseSqlite(...)` in
`src/InventoryApp.Infrastructure/DependencyInjection.cs` and the connection string in
`appsettings.json`. Nothing above the Infrastructure project knows which provider is in use.

---

## 5. Running the MAUI client

**Start the backend first.** The client resolves its address in
`src/InventoryApp.Client/Services/ApiSettings.cs`.

| Target | Address used | Command |
|---|---|---|
| Windows | `https://localhost:7266` | `dotnet build -t:Run -f net9.0-windows10.0.19041.0` |
| Android emulator | `https://10.0.2.2:7266` | `dotnet build -t:Run -f net9.0-android` |
| Android device | `https://<your-LAN-IP>:7266` | see below |
| iOS simulator | `https://localhost:7266` | `dotnet build -t:Run -f net9.0-ios` |
| macOS (Catalyst) | `https://localhost:7266` | `dotnet build -t:Run -f net9.0-maccatalyst` |

Run these from `src/InventoryApp.Client/`. In Visual Studio, set **InventoryApp.Client** as the
startup project and pick the target from the debug dropdown; set **InventoryApp.Api** as a second
startup project so both launch together.

**Physical device:** set `ApiSettings.LanHostFallback` to your development machine's LAN IP
(e.g. `192.168.1.20`), start the API with the `lan` launch profile so it binds to all interfaces
(`dotnet run --project src/InventoryApp.Api --launch-profile lan`), and make sure your firewall
allows port 7266. `ApiSettings.OverrideBaseAddress` can also be set at runtime if you want to add
a settings screen.

**Self-signed certificates:** `GrpcChannelProvider` bypasses certificate validation under
`#if DEBUG` only. Release builds enforce validation normally, so use a real certificate in
production.

---

## 6. Sample accounts

| Username | Password | Role | Can do |
|---|---|---|---|
| `admin` | `Admin@123` | Administrator | Everything, including users and deleting products |
| `manager` | `Manager@123` | Inventory Manager | Products, catalogue, stock, purchases, exports |
| `staff` | `Staff@123` | Staff | View inventory, record sales |
| `rmaharjan` | `Staff@123` | Staff (disabled) | Demonstrates a rejected sign-in |

Seed data includes 8 categories, 29 products, 6 suppliers, opening-stock and adjustment
movements, 6 purchase orders across every status, and 8 sales including one cancelled.

---

## 7. Authorization model

Permissions are defined once in `InventoryApp.Contracts/Security/Permissions.cs` and mapped to
roles in `RolePermissions.cs`. Both the API and the client register one ASP.NET Core policy per
permission via the shared `AddInventoryPolicies()` extension, and the permissions travel inside
the JWT as `perm` claims. That means:

- the server enforces with `[Authorize(Policy = Permissions.ManageProducts)]`
- the client hides UI with `<AuthorizeView Policy="@Permissions.ManageProducts">`

…against exactly the same list. Adding a finer-grained permission is a constant plus a line in
the role map; no screen needs editing. Nothing about authorization is hard-coded in a component.

---

## 8. Stock integrity

`Product.CurrentStock` has a **private setter**. The only way to change it is
`ApplyStockDelta`/`SetStock`, and the only caller is `IStockLedger`, which writes a
`StockMovement` audit row in the same operation. Consequences:

- every quantity change has a movement record with before/after balances, reason, user and timestamp
- `UpdateProductRequest` deliberately has no stock field — editing a product cannot alter quantity
- purchases increase stock only on **receive**; sales decrease it on **create**
- overselling is rejected server-side; cancelling a sale writes `SALE_RETURN` movements
- receive/sale/cancel run inside an explicit transaction

---

## 9. What is where

| Concern | File |
|---|---|
| gRPC contracts | `Contracts/Protos/*.proto` |
| Permissions | `Contracts/Security/` |
| Domain rules | `Domain/Entities/Product.cs`, `PurchaseOrder.cs`, `Sale.cs` |
| Business logic | `Application/Services/` |
| Paging/sorting helpers | `Application/Common/Paging.cs` |
| EF configuration + indexes | `Infrastructure/Persistence/Configurations/` |
| Seed data | `Infrastructure/Seed/DatabaseSeeder.cs` |
| Error → gRPC status mapping | `Api/Infrastructure/ExceptionInterceptor.cs` |
| Client transport | `Client/Services/GrpcChannelProvider.cs`, `AuthInterceptor.cs` |
| Typed API façade | `Client/Services/InventoryApiClient.cs` |
| Screens | `Client/Components/Pages/` |

---

## 10. Known limitations / next steps

- **Money on the wire is `double`.** It is `decimal` in the domain and database, rounded to 2dp at
  the boundary. For financial-grade precision, switch the proto fields to a string or a
  units/nanos message.
- **Caching is minimal.** `LookupCache` holds categories and suppliers for 5 minutes in memory.
  The client service layer is structured so that swapping it for a SQLite-backed store would add
  real offline support without touching any component.
- **Charts are hand-rolled.** Fluent UI Blazor v4 has no chart component, so `BarChart.razor`
  renders bars from Fluent design tokens rather than adding a charting dependency.
- **Stock transfer is lightweight** — a location string on the product plus from/to on the
  movement. A true multi-warehouse model needs a `Location` entity and per-location balances.
- **No tests yet.** The application services are the natural first target: they take
  `IInventoryDbContext`, so an EF Core in-memory or SQLite-in-memory provider is enough.
- **.NET 10:** bump the TFMs in each `.csproj`, `global.json`, and the `Microsoft.Extensions.*` /
  EF Core / ASP.NET package majors together. Check the Fluent UI Blazor release notes first —
  v4 targets .NET 8/9.
