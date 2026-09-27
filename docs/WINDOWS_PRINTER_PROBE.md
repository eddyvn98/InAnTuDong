# Windows printer capability probe

The first M2 probe reads printer capabilities through Windows' installed printer driver using `System.Drawing.Printing`.

## Run

List all installed printers:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj
```

Find the best installed match for Epson L3310:

```powershell
dotnet run --project src/PrintAI.WindowsProbe/PrintAI.WindowsProbe.csproj -- L3310
```

The probe outputs JSON with:

- printer name
- default-printer flag
- driver validity
- color support
- duplex support
- supported paper sizes in millimetres
- advertised printer resolutions
- A4 portrait printable area
- A4 hard margins

## Important limitation

These values come from the installed Windows printer driver and its current configuration. They are capability data, not proof that physical output is dimensionally exact.

Before spooler submission is treated as production-ready:

1. open `/api/calibration-a4.png?dpi=300`
2. print at 100% / actual size
3. measure the 100 mm rulers and reference squares
4. record any consistent scale or offset error
5. only then add device-profile compensation if needed

Do not hard-code Epson-specific geometry into the domain layer.
