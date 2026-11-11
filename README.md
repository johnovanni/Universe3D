# Universe3D

Fly through the universe in 3D, centered on the Sun. Universe3D is a Windows desktop app (C#, WPF, OpenGL) that
plots galaxies, galaxy clusters and quasars at their real positions, from our Local Group of galaxies out to
quasars about 13 billion light-years away. Click any object to see what it is, how far away it is, and whether
it is moving toward us or away from us.

It is made for exploring and learning, with astronomically correct positions. It is not a research tool.

## What you can do

- **Pick one of four data sets**, each shipped as a built-in snapshot that works offline:
  1. Local Group and Local Volume: about 870 nearby galaxies with measured distances.
  2. Nearby universe: about 43,500 galaxies of the 2MASS Redshift Survey, out to about 2 billion light-years.
  3. Galaxy clusters: about 2,300 clusters (MCXC X-ray clusters and Abell optical clusters).
  4. Quasars and AGN: the 250,000 brightest of the Million Quasars catalog (the limit can be changed).
- **Navigate like a map**: left-drag rotates, right-drag pans, the wheel zooms (15% per notch), double-click flies
  to an object, Home shows the whole data set, F centers on the selection, Ctrl+F finds an object by name
  (M31, NGC 4486, Coma, Abell 1656, 3C 273 ...).
- **See shapes up close**: galaxies are dots from far away and cross-fade into spirals, ellipticals and irregulars
  as you zoom in. Clusters are translucent spheres sized by their physical radius. The Milky Way is drawn at its
  true place, 8.2 kpc from the Sun.
- **See motion**: color objects by type or by redshift and blueshift, and optionally draw a fine arrow on each
  object (red for receding, blue for approaching). "Observed" arrows show the measured shift; "Peculiar velocity"
  arrows remove the expansion of the universe and show galaxies falling toward Virgo and the Great Attractor.
- **Filter**: maximum distance, object types, a limit on the number of quasars, and a limit on the number of
  galaxy shapes drawn.
- **Learn as you go**: every astronomy term in the window has a plain-language tooltip, and the information panel
  explains each value for the selected object (distance in light-years and Mpc, look-back time, redshift or
  blueshift, velocity, type and shape, data sources).
- **Download latest**: rebuild any data set from the VizieR catalogs. It runs in the background with progress and
  a Cancel button. The picture changes only when the download completes; a failed or cancelled download leaves
  the existing data untouched. "Use built-in snapshot" goes back to the data shipped with the program.

## System requirements

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 (22H2) or Windows 11, 64-bit | Windows 11, 64-bit |
| Runtime | .NET 8 Desktop Runtime | same |
| GPU | **OpenGL 4.1** with current drivers. Examples: Intel UHD Graphics 620 (8th-gen Core, 2017), AMD Radeon Vega 8, NVIDIA GeForce MX150 / GTX 950 | Intel Iris Xe (11th-gen Core, 2020) or AMD Radeon 680M, or any dedicated GPU from the last ~8 years |
| CPU | 64-bit, 2 cores | 4 cores |
| RAM | 8 GB | 16 GB |
| Display | 1366x768 | 1920x1080 or larger |
| Disk | 300 MB (app + built-in snapshots) | plus ~500 MB if every catalog is downloaded |
| Network | Only for "Download latest" | same |

- On a GPU below the recommended class, the app starts with a lower **quasar limit** (100,000) and **galaxy-shape
  limit** (5,000) and says so once in the status bar. Both can be changed in the left panel. The GPU is recognized
  from its OpenGL renderer name, or, for an unknown GPU, from a short frame-time measurement at startup.
- **USB display adapters** (DisplayLink and similar) copy every frame through the CPU, and some reject OpenGL
  windows. The app works on them where possible, but they do not count toward the requirements. The status bar says
  "running on a USB display adapter" while the window is on one.
- If OpenGL 4.1 cannot be started, the app explains what is missing and which graphics it found. If the window is
  on a USB display adapter, move it to a screen driven directly by the GPU, such as the laptop's own display.

## Build, run and test

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```bash
# build everything (Release)
dotnet build Universe3D.sln -c Release

# run the app
dotnet run --project src/Universe3D.App -c Release

# run the unit tests (no network, no timing checks)
dotnet test --filter "Category!=Network&Category!=Perf"

# run the performance tests (timing checks for picking and shape selection)
dotnet test --filter "Category=Perf"
```

Two command-line modes are useful for development:

```bash
# open straight onto a view: data set (0-3), object, distance in Mpc, arrows, frame ...
dotnet run --project src/Universe3D.App -c Release -- --view "dataset=0;goto=M 31;dist=0.05"

# measure the frame rate of every data set and write a CSV
dotnet run --project src/Universe3D.App -c Release -- --bench bench.csv
```

The built-in snapshots are rebuilt from VizieR with the snapshot builder:

```bash
dotnet run --project tools/Universe3D.SnapshotBuilder -c Release -- src/Universe3D.Core/Data/Embedded
```

(Pass `--quasar-cap N` to change how many quasars are kept, and a data-set name such as `clusters` to rebuild
just one.) Downloaded copies are stored in `%LOCALAPPDATA%\Universe3D`.

### Project layout

- `src/Universe3D.Core`: cosmology, sky coordinate frames, morphology, catalog builders, the VizieR client,
  snapshot storage, display rules (GPU class, USB adapters, OpenGL message) and the glossary. No UI.
- `src/Universe3D.Rendering`: OpenTK renderers (point clouds, galaxy shapes, clusters, Milky Way, scale rings,
  velocity arrows) and the orbit camera.
- `src/Universe3D.App`: the WPF window, view models (CommunityToolkit.Mvvm) and the scene controller.
- `tests/Universe3D.Tests`: xUnit tests, including accuracy checks against reference values.
- `tools/Universe3D.SnapshotBuilder`: rebuilds the built-in snapshots.
- `docs/`: the design spec and performance notes.

All data goes through the `IDataSetStore` interface (see "Future option: SQLite" below); no other code opens
data files.

## Data sources

All catalogs come from [VizieR](https://vizier.cds.unistra.fr/), queried over HTTP: first the Harvard mirror
(`vizier.cfa.harvard.edu`), then the CDS server (`vizier.cds.unistra.fr`). If both fail, the app keeps the
data it has and shows the reason.

| Data set | Catalogs |
|---|---|
| Local Group and Local Volume | [Karachentsev et al. 2013, UNGC](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=J/AJ/145/101) (`J/AJ/145/101`); [McConnachie 2012, Local Group dwarfs](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=J/AJ/144/4) (`J/AJ/144/4`) |
| Nearby universe | [2MASS Redshift Survey, Huchra et al. 2012](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=J/ApJS/199/26) (`J/ApJS/199/26`) |
| Galaxy clusters | [MCXC, Piffaretti et al. 2011](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=J/A%2bA/534/A109) (`J/A+A/534/A109`); [Abell, Abell and Corwin catalogs](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=VII/110A) (`VII/110A`) |
| Quasars and AGN | [Million Quasars catalog, Flesch](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=VII/294) (`VII/294`) |
| Enrichment (position angle, size, distance) | [HyperLEDA](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=VII/237) (`VII/237`); [Cosmicflows-4, Tully et al. 2023](https://vizier.cds.unistra.fr/viz-bin/VizieR?-source=J/ApJ/944/94) (`J/ApJ/944/94`) |

Where a galaxy is in both UNGC and McConnachie, McConnachie wins (it has position angle and ellipticity).
Catalog rows with missing or invalid positions or redshifts are skipped and counted in the status bar.

## How distances and velocities are computed

**Distance.** Each object uses the best available source, in this order:

1. A **measured distance**: McConnachie, then UNGC, then Cosmicflows-4 (from its distance modulus,
   `d = 10^((DM - 25)/5)` Mpc).
2. Otherwise a **comoving distance from redshift** in a flat Lambda-CDM universe with H0 = 70 km/s/Mpc,
   Omega_m = 0.3 and Omega_Lambda = 0.7: `D_C = (c/H0) * integral from 0 to z of dz'/E(z')`, with
   `E(z) = sqrt(Omega_m (1+z)^3 + Omega_Lambda)`. For galaxies with z < 0.03 the redshift is first converted to
   the CMB frame (dipole: 369.8 km/s toward l = 264.0, b = 48.3).

Every object records which of these it used, and the information panel shows it. Look-back time is the
light-travel time implied by the distance. Positions are Sun-centered, in Mpc, and are shown in the
supergalactic frame by default (Galactic and Equatorial are options).

**Velocity.** Every object shows z, the heliocentric radial velocity cz, and a plain-language line such as
"Blueshifted: approaching at 300 km/s". For galaxies with a measured distance the panel also shows the
**peculiar velocity**, which removes the expansion of the universe, in the convention of the catalog that
measured the distance: `v_pec = v_CMB - 74.6 * d` for Cosmicflows-4 (its own Hubble constant; only up to 150 Mpc,
beyond which single distances are too uncertain), and `v_pec = v_LG - 73 * d` in the Local Group frame for UNGC
and McConnachie distances. The panel names the frame. A redshift only
measures motion along our line of sight, so a velocity arrow always lies on the line from the Sun to the object:
outward when receding, inward when approaching. Its length is `L = clamp(k * log10(1 + |v|/100 km/s), 3, 60)`
pixels (k = 18 by default, adjustable).

**Galaxy shapes and the disk-tilt assumption.** The position angle and axis ratio of a galaxy on the sky tell us
how tilted its disk is, but not which edge is nearer to us. Universe3D always makes the same choice: **the disk's
north edge is drawn tilted away from us**. A galaxy's real tilt may be the mirror image of what is drawn. Galaxies
without a measured position angle get an illustrative orientation, and the information panel says so.

## Future option: SQLite

Data sets are stored as compressed snapshot files behind a small `IDataSetStore` interface, and every object has
a stable `Id` (`<catalog>:<identifier>`). Nothing outside a store touches a data file. That keeps a SQLite store
cheap to add later, for browsing a data set as a table, exporting subsets to CSV, running ad-hoc queries, or
keeping the raw catalogs so data sets can be rebuilt without downloading again. It is not built yet; see the
design spec for the details.

## Design

The full design (data sets, physics, rendering, error handling and tests) is in
[docs/superpowers/specs/2026-11-10-universe3d-design.md](docs/superpowers/specs/2026-11-10-universe3d-design.md).
