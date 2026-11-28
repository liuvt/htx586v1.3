PDF fixed font setup (Times New Roman)

The passenger-contract PDF template uses Times New Roman.
To make rendering identical on Linux/VPS, copy your LICENSED Times New Roman files here before publish:

  times.ttf    = Times New Roman Regular
  timesbd.ttf  = Times New Roman Bold
  timesi.ttf   = Times New Roman Italic
  timesbi.ttf  = Times New Roman Bold Italic

Typical Windows source folder:
  C:\Windows\Fonts\

Do not rename the files after copying unless you also update appsettings.json.
The application will prefer these files. If a file is missing, it falls back to the configured font family.
