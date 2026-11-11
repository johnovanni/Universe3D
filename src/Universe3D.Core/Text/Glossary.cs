namespace Universe3D.Core.Text;

/// <summary>Plain-language explanations of every astronomy term shown in the UI, used for tooltips.</summary>
public static class Glossary
{
    private static readonly Dictionary<string, string> Terms = new()
    {
        ["Redshift"] = "How much an object's light is stretched toward red because it is moving away from us. Written z; a bigger z means a faster, and usually more distant, object.",
        ["Blueshift"] = "How much an object's light is squeezed toward blue because it is moving toward us. Only a few nearby galaxies, such as Andromeda, are blueshifted.",
        ["Observed"] = "What telescopes measure: the galaxy's own motion plus the expansion of the universe, which carries distant galaxies away from us.",
        ["PeculiarVelocity"] = "The galaxy's own motion through space, with the universe's expansion removed. 'Peculiar' means particular to this galaxy, not strange. It is usually caused by the gravity of nearby galaxies and clusters.",
        ["ComovingDistance"] = "Distance measured on a grid that expands along with the universe, so it stays fixed for a galaxy that is only carried along by the expansion. It is the distance plotted here.",
        ["LookbackTime"] = "How long the light has been travelling to reach us, which is also how far back in time we see the object. A lookback time of 2 billion years means we see it as it was 2 billion years ago.",
        ["HubbleType"] = "A galaxy's shape class: elliptical, lenticular, spiral, barred spiral or irregular. Astronomers write it as a short code such as E, S0, Sb or SBc.",
        ["AxisRatio"] = "The width of a galaxy's apparent outline divided by its length. A value of 1 looks round; a small value looks thin, like a disk seen edge-on.",
        ["PositionAngle"] = "The direction the long axis of a galaxy's outline points on the sky, in degrees measured from north through east.",
        ["Inclination"] = "How far a galaxy's disk is tilted away from face-on. 0 degrees means we look straight down onto it; 90 degrees means we see it edge-on.",
        ["M500"] = "The mass of a galaxy cluster inside the radius R500, in units of 10^14 solar masses. Most of this mass is dark matter and hot gas.",
        ["R500"] = "The radius around a cluster's center inside which the average density is 500 times the universe's critical density. It is a standard way to say how big a cluster is.",
        ["Quasar"] = "An extremely bright core of a distant galaxy, powered by gas falling into a supermassive black hole. Quasars outshine all the stars in their host galaxy.",
        ["Agn"] = "Active galactic nucleus: a galaxy center that shines brightly because a supermassive black hole is feeding on gas. Quasars are the most luminous kind.",
        ["BlLac"] = "A type of active galaxy whose jet from the central black hole points almost straight at us, making it vary quickly in brightness. Named after the star-like object BL Lacertae.",
        ["Type2Agn"] = "An active galactic nucleus whose bright core is hidden behind a thick ring of dust, so we mainly see the glowing gas around it rather than the core itself.",
        ["Cz"] = "A redshift expressed as a speed: the redshift z multiplied by the speed of light, in kilometers per second. For nearby galaxies it is close to the actual speed of recession.",
        ["Supergalactic"] = "A coordinate system lined up with the flat sheet of nearby galaxies and clusters around the Milky Way, called the Local Supercluster. Its plane shows the structure of our neighborhood most clearly.",
        ["Galactic"] = "A coordinate system lined up with the Milky Way's disk, with the Sun at the center and the galactic center in the direction of zero longitude.",
        ["Equatorial"] = "The sky coordinate system used by most catalogs, based on Earth's equator and poles: right ascension (east-west) and declination (north-south).",
        ["Megaparsec"] = "A unit of distance used for galaxies: one million parsecs, or about 3.26 million light-years. Written Mpc.",
        ["LightYear"] = "The distance light travels in one year, about 9.46 trillion kilometers. Written ly; kly, Mly and Gly mean thousand, million and billion light-years.",
        ["LineOfSightOnly"] = "A redshift or blueshift measures only motion toward or away from us, not sideways motion. So the arrow points along the line between the Sun and the object.",
    };

    public static IReadOnlyDictionary<string, string> All => Terms;

    public static string Get(string key) => Terms[key];
}
