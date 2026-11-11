# Universe3D — Design

**Date:** 2026-11-10
**Status:** Revised after spec review (system requirements, velocity arrows); awaiting final approval
**Sibling project:** SolarSystem3D (`C:\repos\SolarSystem3D`). Universe3D follows its patterns where they fit
(MVVM, built-in catalog snapshot plus a "Download latest" option, the 2D label overlay, and accuracy tests
against reference data).

## 1. Purpose

A Windows desktop app (C#, WPF) for exploring the universe in 3D, centered on the Sun. You choose one
data set at a time, from the Local Group out to quasars ~13 billion light-years away. The app shows where each
object really is and what it is: its redshift or blueshift, velocity, galaxy type and shape. It is for exploring
and learning, with astronomically correct positions; it is not a research tool.

### What the user asked for
- Centered on the Sun, show the 3D positions of as many galaxies as is reasonable, with redshift/blueshift,
  velocity, galaxy type and shape. Include quasars. Clusters are acceptable where there are too many galaxies.
- The same interaction as SolarSystem3D: zoom, rotate and pan. Plus a setting for maximum distance.
- A list box (or similar) that selects **one data set**, which the program then displays.
- Galaxies drawn as **shapes when zoomed in** and as dots when far away.
- An option to show redshift/blueshift as a **fine red or blue arrow** on each object, pointing in the direction
  of its velocity, with length set by the size of the redshift or blueshift (§3, "Velocity arrows").
- No dependence on one particular computer: the spec states **minimum and recommended** systems.
- Graphics: **OpenTK + GLWpfControl** (chosen over Helix's SharpDX variant, which depends on SharpDX, unmaintained since 2018).

### Assumptions (confirmed through the approved design)
- "Accurate" means: measured distances where they exist, cosmologically correct distances otherwise.
- It runs on ordinary Windows PCs and laptops, including those with integrated graphics. The development machine
  (Core i7‑1165G7, Iris Xe, 16 GB) is a "recommended" class system and is used for measuring performance.

### System requirements

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 (22H2) or Windows 11, 64-bit | Windows 11, 64-bit |
| Runtime | .NET 8 Desktop Runtime | same |
| GPU | **OpenGL 4.1** with current drivers. Examples: Intel UHD Graphics 620 (8th-gen Core, 2017), AMD Radeon Vega 8, NVIDIA GeForce MX150 / GTX 950 | Intel Iris Xe (11th-gen Core, 2020) or AMD Radeon 680M, or any dedicated GPU from the last ~8 years |
| CPU | 64-bit, 2 cores | 4 cores |
| RAM | 8 GB | 16 GB |
| Display | 1366×768 | 1920×1080 or larger |
| Disk | 300 MB (app + built-in snapshots) | plus ~500 MB if every catalog is downloaded |
| Network | Only for "Download latest" | same |

- Below the recommended class, the app lowers the **quasar limit** default to 100,000 and the **galaxy-shape cap**
  to 5,000 (both still adjustable). Detection uses the OpenGL renderer string and a short frame-time measurement
  at startup.
- **USB display adapters** (DisplayLink and similar) run every frame through the CPU and some reject OpenGL
  windows. The app works on them where possible, but they don't count toward the requirements. The status bar says
  when the window is on such a display.

### Success criteria
1. All four data sets load offline from built-in snapshots, and each can be refreshed from VizieR.
2. On a **recommended** system's built-in display: galaxy and cluster data sets run at ≥ 50 fps. Quasars run at
   ≥ 30 fps with 250,000 shown and stay usable (≥ 20 fps) with all of them. On a **minimum** system: every data set
   runs at ≥ 25 fps with the reduced defaults.
3. Known objects come out right (see §8): Andromeda, M87/Virgo, Coma, 3C 273, cosmology reference values,
   and supergalactic coordinates.
4. Clicking any object shows its name, type, shape, redshift/blueshift, velocity, distance (light-years, Mpc
   and look-back time) and data sources.

## 2. Data sets

Exactly one data set is active at a time, chosen from a list box. Every data set ships as a built-in snapshot,
and "Download latest" rebuilds it from VizieR.

| # | Data set | Sources (VizieR) | Contents |
|---|---|---|---|
| 1 | Local Group & Local Volume | `J/AJ/145/101/catalog`: Karachentsev+ 2013 UNGC (~870). `J/AJ/144/4/catalog`: McConnachie 2012 Local Group dwarfs | Measured distance, heliocentric velocity (HRV), Local Group velocity, morphological type, a26 diameter, b/a, inclination; PA and ellipticity for McConnachie objects |
| 2 | Nearby universe | `J/ApJS/199/26/table3`: 2MASS Redshift Survey (43,533) | cz (heliocentric), ZCAT type code, b/a, isophotal radius, K magnitude |
| 3 | Galaxy clusters | `J/A+A/534/A109/mcxc`: MCXC (1,743). `VII/110A` Abell north and south tables (only clusters with measured z) | z, M500, R500, L500 (MCXC); richness, Bautz–Morgan type (Abell) |
| 4 | Quasars & AGN | `VII/294/catalog`: Million Quasars v8 (2023) | z, type (Q quasar, A AGN, B BL Lac, K/N type‑II), R and B magnitudes |

Enrichment used while building data sets 1–2:
- **HyperLEDA** `VII/237/pgc` (983,261 galaxies): position angle (PA), logD25 diameter, logR25 axis ratio and
  morphological type, cross-matched by position.
- **Cosmicflows‑4** `J/ApJ/944/94/table2` (~56,000 galaxies): redshift-independent distance modulus (DM),
  CMB-frame velocity, and supergalactic coordinates (SGL/SGB), cross-matched by position.

Rules and limits:
- If a galaxy appears in both UNGC and McConnachie, McConnachie wins for Local Group members, because it has PA
  and ellipticity.
- Quasars: rows without a redshift are dropped. The built-in snapshot keeps the 250,000 brightest by R magnitude,
  falling back to B when R is missing. The **quasar limit** setting can lower this or raise it up to every
  downloaded quasar.
- Abell clusters without a measured redshift are excluded. Abell clusters that MCXC also lists are merged into one
  object, keeping MCXC's physical values and the Abell name.
- VizieR access: the Harvard mirror `https://vizier.cfa.harvard.edu/viz-bin/asu-tsv` first, then
  `https://vizier.cds.unistra.fr/viz-bin/asu-tsv`. (The CDS server was down on 2026-11-10 and the mirror worked.)

## 3. Physics

### Distance
1. **Measured distance**, when available, in this order: McConnachie → UNGC → Cosmicflows‑4 (from DM: `d = 10^((DM−25)/5)` Mpc).
2. Otherwise **comoving distance from redshift**, in a flat ΛCDM cosmology with H₀ = 70 km/s/Mpc, Ωm = 0.3 and
   ΩΛ = 0.7. It is computed by numerical integration of `D_C = (c/H₀) ∫₀^z dz′/E(z′)` with
   `E(z) = sqrt(Ωm(1+z)³ + ΩΛ)`. A z → D_C table (0 ≤ z ≤ 8) is precomputed and interpolated.
   - For galaxies with z < 0.03, the redshift used for distance is the **CMB-frame** redshift: heliocentric cz
     corrected by the CMB dipole (369.8 km/s toward l = 264.0°, b = 48.3°). Objects with CMB-frame cz ≤ 0 and
     no measured distance are placed at the distance their velocity gives in the Local Group frame. If that is
     also ≤ 0, they are excluded from the 3D view and counted in the status bar.
3. Every object stores a **distance source**: Measured (which catalog) or Redshift.
4. Each object also has a **look-back time**: `t_L = (1/H₀) ∫₀^z dz′/((1+z′)E(z′))`.
5. Units shown: light-years (adaptive: ly, kly, Mly, Gly) and Mpc. 1 Mpc = 3.2616 Mly.

### Velocity and redshift
- Every object shows z, heliocentric radial velocity cz, and a plain-language line, for example
  "Blueshifted: approaching at 300 km/s" or "Redshifted: receding at 1,284 km/s".
  For z > 0.1 the line says "recession due to cosmic expansion", and look-back time is the headline figure.
- For galaxies with a measured distance, the panel also shows **peculiar velocity**:
  `v_pec ≈ v_CMB − H₀·d`, using the Cosmicflows‑4 convention, at low z only (z < 0.1).

### Velocity arrows (optional display)
- With **Show velocity arrows** on, each visible object gets a fine arrow: **red** for redshift (receding) and
  **blue** for blueshift (approaching).
- **Direction:** a redshift or blueshift measures only the *radial* velocity, along our line of sight; sideways
  motion is invisible in it. So the arrow points **along the line from the Sun to the object**:
  - outward (away from the Sun) when receding,
  - inward (toward the Sun) when approaching.

  This is shown truthfully rather than inventing a 3D velocity. The info panel says "arrow shows line-of-sight
  motion only".
- **Arrow mode**, a selector:
  - **Observed** (default): the measured redshift or blueshift (heliocentric cz, or z for distant objects). This is
    almost all red beyond the Local Group, because the universe is expanding.
  - **Peculiar**: the radial peculiar velocity `v_pec ≈ v_CMB − H₀·d`, with the expansion of the universe removed.
    Only objects with a measured distance get an arrow (data sets 1–2). This mode shows galaxies falling toward
    Virgo and the Great Attractor, with blue arrows on the far side of a mass pointing back toward it.
- **Length** grows with the size of the shift. It is measured in **screen pixels**, so arrows stay readable at any
  zoom: `L = clamp(k · log10(1 + |v| / 100 km/s), 3, 60)` pixels, where |v| is |cz| (Observed, or c·z when
  z > 0.1) or |v_pec| (Peculiar). An **arrow scale** slider sets k (default 18). Using a log scale keeps both
  ~50 km/s Local Group motions and z ≈ 7 quasars visible together.
- Each arrow is a 1-pixel line with a small open arrowhead, at 70% opacity so dense regions stay readable. Arrows
  follow every filter (maximum distance, type checkboxes).

### Coordinates
- Internal positions are Sun-centered Cartesian vectors in **Mpc in the J2000 equatorial frame**, computed from
  RA, Dec and distance.
- Display frame: **Supergalactic** by default, with Galactic or Equatorial as options. Rotations use the IAU
  definitions (galactic north pole RA 192.85948°, Dec 27.12825°, l of the north celestial pole 122.93192°;
  supergalactic north pole at l = 47.37°, b = 6.32°, with the zero point at l = 137.37°, b = 0°).
- The **Milky Way** sits at its true location: center 8.2 kpc (26,700 ly) from the Sun toward l = 0°, b = 0°, with
  its disk in the galactic plane, drawn as a barred spiral 30 kpc across.

### Morphology
One `MorphologyClass` set for all catalogs: Elliptical, Lenticular, Spiral (Sa–Sd with a subtype), Barred spiral,
Irregular, Dwarf spheroidal, Dwarf irregular, Peculiar/merger, Unknown. Each catalog's code is parsed into it:
- ZCAT/2MRS type codes, such as `3A2s` and `5X_s`
- UNGC numeric T-types
- McConnachie MType strings, such as `dSph` and `dIrr`
- HyperLEDA MType strings, such as `Sbc` and `E`

The original code is always kept and shown as well.

### Shape and orientation
- Apparent shape: PA (east of north) and axis ratio q = b/a, preferring HyperLEDA, then McConnachie, then the
  catalog's own b/a. Diameter: a26 (UNGC), then D25 (HyperLEDA), then the isophotal radius (2MRS). It is
  converted to kpc using the object's distance.
- **Inclination**: UNGC's measured value if present. Otherwise `cos²i = (q² − q₀²)/(1 − q₀²)`, with q₀ = 0.2
  for disks, clamped at i = 90°.
- **Disk orientation**: the disk normal is built in the local sky frame (east, north, line of sight) from i and PA,
  then rotated into equatorial coordinates. Whether the near side tilts north or south can't be determined from
  imaging; we always use the same choice (normal tilted toward +north), and the info panel says so.
- Ellipticals are ellipsoids with axis ratio q, their long axis lying along the PA on the sky.

## 4. Rendering (Universe3D.Rendering)

- **OpenTK 4.9 + GLWpfControl 4.3**, OpenGL 4.1 core profile minimum (Iris Xe supports 4.6).
- **Point clouds**: one vertex buffer per data set, uploaded once. Per vertex: position (float32 Mpc in the
  current display frame), a morphology/type code, velocity (cz), brightness and object index.
  - The vertex shader applies the **maximum distance** and **type mask** as uniforms (discarding filtered objects)
    and picks the color from the **color mode** uniform (type or velocity). The sprite size comes from brightness.
  - The fragment shader draws a soft round sprite. Quasars use additive blending; galaxies use alpha blending.
  - Changing filters never re-uploads data.
- **Galaxy shapes**: instanced rendering of a disk mesh and an ellipsoid mesh. Per instance: position,
  orientation quaternion, size (kpc → Mpc), axis ratio and type.
  - The disk fragment shader draws procedural spiral arms with a bulge (for spirals), a smooth gradient
    (lenticulars) or noise patches (irregulars).
  - Instances are chosen on the CPU when the camera moves (projected size > 4 px, at most 20,000 drawn). The dot
    and the shape cross-fade between 4 and 8 px.
- **Velocity arrows** (optional): drawn in one pass from the point-cloud buffer, whose per-vertex data also
  carries signed radial and peculiar velocities. A geometry shader turns each visible point into a screen-space
  line plus a 2-segment arrowhead along the projected Sun→object direction, using the length formula in §3.
  If the projected direction is under 1 pixel (an object straight ahead), the arrowhead is drawn as a small
  ⊙ (approaching) or ⊗ (receding) marker instead. Nothing is re-uploaded when the mode or scale changes.
- **Clusters**: instanced translucent spheres with radius R500 (or 1.5 Mpc when unknown), plus a core point.
- **Milky Way**: a procedural barred-spiral disk with the Sun marker.
- **Scale rings**: concentric rings in the display frame's reference plane at 1–3–10 spacing (1 Mly, 3 Mly,
  10 Mly, …), showing the decades around the current zoom level, labeled through the overlay.
- **Camera**: an orbit camera in double precision (target, yaw, pitch, logarithmic distance from 10⁻⁴ to 10⁵ Mpc).
  Controls:
  - Left-drag rotates, right-drag pans, the wheel zooms by 15% per notch.
  - Double-click flies to an object over 0.8 s.
  - Home resets the view to fit the data set; F centers on the selection.
  - Matrices are double precision on the CPU, and float relative-to-eye on the GPU (positions minus the camera
    target), so zooming into distant galaxies keeps full precision.
- **Picking**: a CPU projection of all *visible* objects on click (the closest within 8 px, preferring shapes
  under the cursor). Up to 1 million objects take under 15 ms.
- **Labels and selection ring**: the WPF 2D overlay from SolarSystem3D (`LabelOverlay`, pooled TextBlocks with
  collision avoidance).
  - Priority: selected object, Milky Way, Messier/NGC-named galaxies (by brightness), named clusters,
    3C/PKS-named quasars, then other objects by brightness.
  - At most 60 labels per frame.

## 5. User interface (Universe3D.App)

The layout follows SolarSystem3D: a dark theme, a left control panel, the 3D view in the center, a right
information panel and a bottom status bar.

- **Left panel**:
  - The **data-set list box**, with each set's object count and distance range.
  - **Search** by name (M31, NGC 4486, Coma, Abell 1656, 3C 273).
  - The **maximum-distance slider**, logarithmic, labeled in light-years and Mpc, with its range set per data set.
  - **Type checkboxes**, whose contents depend on the data set: galaxy classes, cluster sources, quasar types.
  - **Color mode** (type or velocity) and **frame** (supergalactic, galactic or equatorial).
  - Toggles for galaxy shapes, names, scale rings and the Milky Way.
  - **Velocity arrows**: the show/hide toggle, the **arrow mode** (Observed or Peculiar), and the **arrow scale**
    slider. Peculiar mode is disabled, with a tooltip, for data sets that have no measured distances.
  - The **quasar limit** (only for data set 4).
  - **Catalogs**: "Download latest" and "Use built-in snapshot", with status text.
- **Right panel**, for the selected object:
  - Name, also-known-as names, type (class and original code) and shape (PA, b/a, inclination, diameter).
  - Redshift/blueshift line, z, cz, CMB-frame velocity, and peculiar velocity when available.
  - Distance in light-years and Mpc, its source, and look-back time.
  - For clusters: mass M500 and radius R500. For quasars: magnitudes and type.
  - The data source catalogs.
- **Status bar**: data set, objects shown / loaded, objects excluded (with no valid distance), camera distance,
  fps, and warnings (for example "running on a USB display adapter" or "reduced defaults for this GPU").
- Keyboard: Home (fit), F (focus), Ctrl+F (search).

## 6. Code structure

```
C:\repos\Universe3D
  Universe3D.sln
  src/Universe3D.Core          (net8.0; no UI)
    Cosmology/   Cosmology (ΛCDM tables), RedshiftMath (z↔v, CMB dipole, Local Group frame)
    Coordinates/ Vector3d, Matrix3d, SkyFrames (equatorial/galactic/supergalactic), SkyPosition
    Objects/     CosmicObject, Galaxy, GalaxyCluster, Quasar, MilkyWay, DistanceSource, DataSetKind
    Morphology/  MorphologyClass, MorphologyParser (ZCAT, T-type, McConnachie, HyperLEDA)
    Shapes/      GalaxyOrientation (inclination, disk normal, quaternion)
    Catalogs/    VizierClient (mirror fallback), TsvTable, SkyCrossMatcher (spatial hash),
                 DataSetBuilder + one builder per data set, SnapshotSerializer (gzip binary),
                 SnapshotStore (embedded + %LOCALAPPDATA%\Universe3D)
    Data/Embedded/  the four built-in snapshots (*.u3d.gz)
  src/Universe3D.Rendering     (net8.0-windows; OpenTK, GLWpfControl)
    GlHost, ShaderProgram, Buffers, PointCloudRenderer, VelocityArrowRenderer, ShapeRenderer, ClusterRenderer,
    MilkyWayRenderer, ScaleRingRenderer, OrbitCamera, Projector, Picker, Shaders/*.glsl (embedded)
  src/Universe3D.App           (net8.0-windows; WPF, CommunityToolkit.Mvvm)
    ViewModels/ (MainViewModel, DataSetViewModel, FilterOptions, InfoViewModel), Controllers/SceneController,
    Overlay/LabelOverlay, MainWindow.xaml
  tools/Universe3D.SnapshotBuilder  (console app; runs the Core builders to regenerate built-in snapshots)
  tests/Universe3D.Tests       (xUnit)
```

- The **same builders** create the built-in snapshots (through the console tool) and serve "Download latest" in
  the app. Downloads run in the background with progress reporting and cancellation.
- `Directory.Build.props` contains the same short-path rule as SolarSystem3D, so builds also work in deep folders.

## 7. Error handling

- **Network or VizieR failure**: try the mirror, then CDS. If both fail, keep the cached or built-in snapshot and
  show the reason in the catalog status text.
- **Malformed rows** (missing RA, Dec or z, out-of-range values): skip them, count them, and report the count in the
  build summary and status bar.
- **OpenGL context failure or version below 4.1**: show a message naming the requirement and the detected
  renderer, with a hint to move the window to a screen driven directly by the GPU if it's on a USB display
  adapter. Don't crash.
- **Weaker GPU detected**: apply the reduced defaults from §1 and mention it once in the status bar.
- **Corrupt snapshot in the cache**: delete it and fall back to the built-in snapshot, with a status message.

## 8. Testing

xUnit tests in `Universe3D.Tests`:
- **Cosmology** (H₀ = 70, Ωm = 0.3, flat). Comoving distance and look-back time must match these values within
  0.1%. They were computed independently with Simpson integration (20,000 steps) on 2026-11-10:

  | z | D_C (Mpc) | t_L (Gyr) |
  |---|---|---|
  | 0.1 | 418.5 | 1.301 |
  | 1 | 3,303.8 | 7.715 |
  | 3 | 6,355.7 | 11.354 |
  | 7 | 8,627.6 | 12.716 |
- **Frames**:
  - The galactic north pole and galactic center map to their IAU equatorial positions within 0.001°.
  - Supergalactic SGL/SGB computed for 200 Cosmicflows‑4 galaxies match the catalog's published values within 0.01°.
- **Known objects** (from the built-in snapshots):
  - M31: distance 0.7–0.8 Mpc, cz ≈ −300 km/s, shown as blueshifted.
  - M87: 15–18 Mpc.
  - Coma cluster: z within 0.002 of 0.0231.
  - 3C 273: z within 0.001 of 0.158.
  - Milky Way center: 8.2 kpc from the Sun toward l = 0°.
- **Morphology parser**: a table of codes from each catalog mapped to the expected class.
- **Shape round trip**: building a disk from (PA, q) and projecting it onto the sky gives back PA within 0.5° and
  q within 0.01 (for q > q₀).
- **Cross-matcher**: synthetic catalogs with known pairs, including pairs near RA 0/360 and the poles.
- **Snapshot serializer**: a save/load round trip of every field.
- **Velocity arrows** (the CPU-side reference function that mirrors the shader math):
  - Andromeda's arrow points toward the Sun and is blue; Coma's points away and is red.
  - Length grows with |v| and is clamped to 3–60 px.
  - Peculiar mode gives no arrow to objects without a measured distance.
- **Visual and performance checks** (manual, with screenshots as in SolarSystem3D):
  - Frame rate for each data set, including quasars at 250,000 and all, with velocity arrows on and off.
  - Done on the development machine's built-in display (recommended class) and, as a stress case, on a USB
    display adapter, as milestone 3, before shapes and UI are built on top of the renderer.
  - The minimum class is checked with the reduced defaults by capping the GPU frame budget. If an 8th-gen Intel
    UHD 620 machine is available, it is also measured on that.

## 9. Build order (detailed in the implementation plan)

1. Core math: cosmology, frames, morphology, shapes, and their tests.
2. Data pipeline: VizieR client, cross-matcher, builders, snapshots, and the built-in snapshots.
3. A minimal OpenGL point renderer, camera and velocity arrows, plus the **performance check** (§8).
4. Galaxy shapes, cluster spheres, Milky Way and scale rings.
5. UI: data-set selector, filters, search, picking, info panel and names.
6. Polish, README, and an optional GitHub repository (asked at the time).

## 10. Out of scope

- Time evolution (the universe is shown as observed today, with no animation).
- Galaxy images or photo textures (shapes are procedural).
- Distances from photometric redshifts; only spectroscopic or measured distances are used.
- Galaxies beyond 2MRS depth (deep surveys such as SDSS cover only part of the sky).
