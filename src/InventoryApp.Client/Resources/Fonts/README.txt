Place OpenSans-Regular.ttf and OpenSans-Semibold.ttf here.

They are registered in MauiProgram.cs via ConfigureFonts(). These fonts style the native
MAUI chrome only - the Blazor UI inside the WebView uses the Fluent UI type ramp and the
Segoe UI Variable stack declared in wwwroot/css/app.css.

The default .NET MAUI project template ships these two files; copy them from any freshly
created MAUI project, or download Open Sans from https://fonts.google.com/specimen/Open+Sans.
If you would rather not add them, delete the two fonts.AddFont(...) lines in MauiProgram.cs.
