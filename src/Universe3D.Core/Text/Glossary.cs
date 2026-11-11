using Universe3D.Core.Objects;

namespace Universe3D.Core.Text;

/// <summary>Plain-language explanations of every astronomy term shown in the UI, used for tooltips.</summary>
public static class Glossary
{
    private static readonly Dictionary<string, string> Terms = new()
    {
        ["Redshift"] = "How much an object's light is stretched toward longer, redder wavelengths, written z. Two things cause it: the galaxy's own motion through space, which matters most for the nearest galaxies, and the expansion of space, which stretches the light while it travels and dominates for everything farther away. So a bigger z generally means a more distant object whose light set out longer ago.",
        ["Blueshift"] = "How much an object's light is squeezed toward blue because it is moving toward us. Only a few nearby galaxies, such as Andromeda, are blueshifted.",
        ["Observed"] = "What telescopes measure: the galaxy's own motion plus the expansion of the universe, which carries distant galaxies away from us.",
        ["PeculiarVelocity"] = "The galaxy's own motion through space, with the universe's expansion removed. 'Peculiar' means particular to this galaxy, not strange. It is usually caused by the gravity of nearby galaxies and clusters.",
        ["ComovingDistance"] = "Distance measured on a grid that expands along with the universe, so it stays fixed for a galaxy that is only carried along by the expansion. Objects whose distance comes from their redshift are placed at this distance.",
        ["LookbackTime"] = "How long the light has been travelling to reach us, which is also how far back in time we see the object. A lookback time of 2 billion years means we see it as it was 2 billion years ago.",
        ["HubbleType"] = "A galaxy's shape class: elliptical, lenticular, spiral, barred spiral or irregular. Astronomers write it as a short code such as E, S0, Sb or SBc.",
        ["AxisRatio"] = "The width of a galaxy's apparent outline divided by its length. A value of 1 looks round; a small value looks thin, like a disk seen edge-on.",
        ["PositionAngle"] = "The direction the long axis of a galaxy's outline points on the sky, in degrees measured from north through east.",
        ["Inclination"] = "How far a galaxy's disk is tilted away from face-on. 0 degrees means we look straight down onto it; 90 degrees means we see it edge-on.",
        ["M500"] = "The mass of a galaxy cluster inside the radius R500, in units of 10^14 solar masses. Most of this mass is dark matter; most of the rest is hot gas, and the stars in the galaxies are only a small part.",
        ["R500"] = "The radius around a cluster's center inside which the average density is 500 times the universe's critical density. It is a standard way to say how big a cluster is.",
        ["Quasar"] = "An extremely bright core of a distant galaxy, powered by gas falling into a supermassive black hole. Quasars outshine all the stars in their host galaxy.",
        ["Agn"] = "Active galactic nucleus: a galaxy center that shines brightly because a supermassive black hole is feeding on gas. Quasars are the most luminous kind.",
        ["BlLac"] = "A type of active galaxy whose jet from the central black hole points almost straight at us, making it vary quickly in brightness. Named after the star-like object BL Lacertae.",
        ["Type2Agn"] = "An active galactic nucleus whose bright core is hidden behind a thick ring of dust, so we mainly see the glowing gas around it rather than the core itself.",
        ["Cz"] = "The redshift z multiplied by the speed of light, in kilometers per second: a way to write a redshift as a speed. For distant galaxies it approximates how fast the expansion of the universe carries them away. For the nearest galaxies their own motion dominates, so cz can even be negative: Andromeda's is about -300 km/s because it is coming toward us.",
        ["Supergalactic"] = "A coordinate system lined up with the flat sheet of nearby galaxies and clusters around the Milky Way, called the Local Supercluster. Its plane shows the structure of our neighborhood most clearly.",
        ["Galactic"] = "A coordinate system lined up with the Milky Way's disk, with the Sun at the center and the galactic center in the direction of zero longitude.",
        ["Equatorial"] = "The sky coordinate system used by most catalogs, based on Earth's equator and poles: right ascension (east-west) and declination (north-south).",
        ["Megaparsec"] = "A unit of distance used for galaxies: one million parsecs, or about 3.26 million light-years. Written Mpc.",
        ["LightYear"] = "The distance light travels in one year, about 9.46 trillion kilometers. Written ly; kly, Mly and Gly mean thousand, million and billion light-years.",
        ["MeasuredDistance"] = "A distance found without using the redshift, for example from the brightness of certain stars or from how fast a galaxy spins. It does not depend on the galaxy's own motion, which is why it can be used to find peculiar velocities.",
        ["CmbFrame"] = "The velocity measured relative to the cosmic microwave background, the afterglow of the Big Bang. It removes the Sun's own motion (about 370 km/s) so that velocities from all directions can be compared.",
        ["Magnitude"] = "How bright an object looks from Earth on a backwards scale: smaller numbers are brighter, and each step of 5 magnitudes is a factor of 100 in brightness. R and B mean red and blue filters.",
        ["L500"] = "The X-ray power given off by a cluster's hot gas inside the radius R500, in units of 10^37 watts. Gas at tens of millions of degrees shines in X-rays.",
        ["Richness"] = "Abell's count of how many bright galaxies a cluster has, grouped into classes from 0 (30 to 49 galaxies) up to 5 (300 or more).",
        ["BautzMorgan"] = "A cluster type by how much its brightest galaxy dominates: type I has one giant central galaxy, type III has no dominant galaxy, and type II is in between.",
        ["Mcxc"] = "The Meta-Catalogue of X-ray detected Clusters: about 1,700 clusters found by the X-rays from their hot gas, with masses and sizes worked out from that X-ray light.",
        ["Abell"] = "George Abell's catalog of rich galaxy clusters, found by eye on photographic sky survey plates by counting galaxies. Only Abell clusters with a measured redshift are shown.",
        ["Elliptical"] = "A smooth, round or oval galaxy of mostly old stars, with little gas or dust and no disk or spiral arms.",
        ["Lenticular"] = "A lens-shaped galaxy (type S0) with a disk and a central bulge, like a spiral galaxy without spiral arms.",
        ["Spiral"] = "A galaxy with a flat rotating disk and spiral arms where new stars form, around a central bulge. Sa spirals have big bulges and tight arms; Sd spirals have small bulges and loose arms.",
        ["BarredSpiral"] = "A spiral galaxy whose arms start from the ends of a straight bar of stars across its center. The Milky Way is one.",
        ["Irregular"] = "A galaxy with no clear shape, often rich in gas and young stars.",
        ["DwarfSpheroidal"] = "A small, faint, roundish galaxy of old stars with almost no gas. Many orbit the Milky Way and Andromeda.",
        ["DwarfIrregular"] = "A small galaxy with no clear shape, usually rich in gas and still forming stars.",
        ["PeculiarGalaxy"] = "A galaxy with an unusual, disturbed shape, often because it is colliding or merging with another galaxy.",
        ["Unclassified"] = "The source catalog gives no shape class for these galaxies, often because they are too faint or small in the survey images. They are drawn as plain disks.",
        ["DiskTilt"] = "From a picture we can tell how tilted a galaxy's disk is, but not which edge is nearer to us. This program always draws the disk with its north edge tilted away from us, so the true tilt may be the mirror image.",
        ["Diameter"] = "The galaxy's size across, measured where its light fades to a standard faint level, converted to light-years using its distance.",
        ["ScaleRings"] = "Circles around the Sun at 1, 3, 10, 30, ... times a unit of light-years, drawn in the reference plane of the chosen coordinate system, to show the scale.",
        ["MilkyWay"] = "Our own galaxy, a barred spiral about 100,000 light-years across. The Sun is about 26,700 light-years from its center.",
        ["LocalGroup"] = "The Local Group is the small family of about 100 galaxies that includes the Milky Way, Andromeda and Triangulum, spread over about 10 million light-years. The Local Volume adds the nearby galaxy groups around it, such as those of Centaurus A and M81.",
        ["QuasarLimit"] = "Shows only the brightest quasars and active galaxies, chosen by their R magnitude (or B where R is missing). Fewer objects draw faster on slower graphics cards.",
        ["NoValidDistance"] = "Objects that cannot be placed in 3D: they have no measured distance, and their velocity is zero or negative (approaching) both relative to the cosmic microwave background and relative to the Local Group, so their redshift gives no distance.",
        ["LineOfSightOnly"] = "A redshift or blueshift measures only motion toward or away from us, not sideways motion. So the arrow points along the line between the Sun and the object.",
    };

    public static IReadOnlyDictionary<string, string> All => Terms;

    public static string Get(string key) => Terms[key];

    /// <summary>Keys for the "Types" filter header: galaxy types, cluster catalogs or quasar types (spec §5).</summary>
    public static string TypesHeaderKeys(DataSetKind kind) => kind switch
    {
        DataSetKind.Clusters => "Mcxc Abell",
        DataSetKind.Quasars => "Quasar Agn BlLac Type2Agn",
        _ => "HubbleType",
    };
}
