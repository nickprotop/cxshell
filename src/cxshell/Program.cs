using cxshell.Desktop;

if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

return await new DesktopShell().RunAsync();
