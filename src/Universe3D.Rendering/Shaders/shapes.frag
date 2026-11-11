#version 410 core
// Galaxy shapes. The view ray is intersected with the ellipsoid in its unit-sphere space; the chord's midpoint
// gives the position in the disk plane for the pattern, and the closest approach to the center (in true
// proportions) gives a round bulge. Patterns: spiral arms with a bulge, a bar, a smooth lenticular gradient
// (also used for unclassified galaxies), noise patches for irregulars, and a concentrated glow for ellipsoids.
// Drawn with additive blending.

flat in vec3 v_origin;
in vec3 v_dir;
flat in vec3 v_shape;
flat in vec4 v_color;
flat in uint v_kind;
flat in float v_seed;

out vec4 o_color;

const uint KindEllipsoid = 0u;
const uint KindSpiral = 1u;
const uint KindBarred = 2u;
const uint KindLenticular = 3u;
const uint KindIrregular = 4u;

const vec3 BulgeColor = vec3(1.0, 0.86, 0.62);

// Hash without sine (stable in float precision for large inputs).
float hash(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p)
{
    // Each octave rotated, so the value-noise grid does not show.
    const mat2 turn = mat2(0.8, 0.6, -0.6, 0.8);
    float s = 0.0, a = 0.5;
    for (int k = 0; k < 4; k++) { s += a * noise(p); p = turn * p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
    return s;
}

// Logarithmic two-armed spiral starting at radius r0, 0..1 on the arms.
float arms(vec2 xy, float r, float r0, float phase)
{
    float th = atan(xy.y, xy.x);
    float a = 0.5 + 0.5 * cos(2.0 * (th - 3.4 * log(max(r, 1e-3) / r0)) + phase);
    return a * a * a;
}

void main()
{
    vec3 o = v_origin, d = v_dir;
    float a = dot(d, d);
    vec3 c = cross(o, d);
    float h2 = dot(c, c) / a;                  // squared distance from the center to the ray (unit space)
    if (h2 >= 1.0) discard;
    float tm = -dot(o, d) / a;
    float hw = sqrt((1.0 - h2) / a);
    float t1 = tm + hw;
    if (t1 <= 0.0) discard;                    // behind the eye
    float t0 = max(tm - hw, 0.0);              // from the eye when it is inside
    vec3 m = o + d * (0.5 * (t0 + t1));        // chord midpoint
    float chord = 0.5 * (t1 - t0) * sqrt(a);   // 1 through the center

    float intensity;
    float warm;
    if (v_kind == KindEllipsoid)
    {
        float r = sqrt(h2);
        intensity = pow(1.0 - h2, 1.5) * (0.25 + 1.1 * exp(-6.0 * r));
        warm = 0.55;
    }
    else
    {
        // Round bulge from the ray's closest approach to the center, in units of the disk radius.
        vec3 os = o * v_shape, ds = d * v_shape;
        float tb = clamp(-dot(os, ds) / dot(ds, ds), t0, t1);
        vec3 cb = os + ds * tb;
        float b2 = dot(cb, cb);

        vec2 xy = m.xy;
        float r = length(xy);
        float edge = 1.0 - smoothstep(0.65, 1.0, r);
        // Pattern contrast fades as the disk turns edge-on, where the chord midpoint would smear it into stripes.
        float faceOn = abs(normalize(ds).z);
        float contrast = smoothstep(0.08, 0.45, faceOn);
        float disk;
        float bulge;
        if (v_kind == KindSpiral)
        {
            float s = arms(xy, r, 0.07, 6.2832 * v_seed);
            float clumps = 0.75 + 0.5 * noise(xy * 14.0 + v_seed * 31.0);
            disk = exp(-r / 0.32) * mix(1.0, (0.2 + 1.9 * s) * clumps, contrast * smoothstep(0.05, 0.16, r));
            bulge = 1.3 * exp(-b2 / (2.0 * 0.08 * 0.08));
        }
        else if (v_kind == KindBarred)
        {
            float s = arms(xy, r, 0.3, 0.0);   // the arms start at the bar's ends (local x)
            float clumps = 0.75 + 0.5 * noise(xy * 14.0 + v_seed * 31.0);
            float bar = exp(-(xy.x * xy.x / (2.0 * 0.17 * 0.17) + xy.y * xy.y / (2.0 * 0.04 * 0.04)));
            disk = exp(-r / 0.34) * mix(mix(0.8, 0.45, contrast), (0.2 + 1.9 * s) * clumps, contrast * smoothstep(0.24, 0.36, r))
                   + 0.9 * contrast * bar;
            bulge = 1.1 * exp(-b2 / (2.0 * 0.07 * 0.07));
        }
        else if (v_kind == KindIrregular)
        {
            float patches = smoothstep(0.35, 0.8, fbm(xy * 3.2 + vec2(v_seed * 17.0, v_seed * 7.0)));
            disk = exp(-r * r / 0.32) * mix(1.2, 0.5 + 2.0 * patches, contrast);
            bulge = 0.0;
        }
        else // KindLenticular, and unclassified galaxies
        {
            disk = 1.2 * exp(-r / 0.24);
            bulge = 0.9 * exp(-b2 / (2.0 * 0.12 * 0.12));
        }
        float diskLight = disk * edge * (0.35 + 0.65 * chord);
        intensity = diskLight + bulge;
        warm = bulge / (intensity + 1e-4);
    }

    vec3 rgb = mix(v_color.rgb, BulgeColor, clamp(warm, 0.0, 1.0));
    // Soft saturation: a hard clamp at 1 leaves flat plateaus with sharp edges in bright bars and bulges.
    o_color = vec4(rgb, (1.0 - exp(-1.3 * intensity)) * v_color.a);
}
