# Test fixtures

Captured from the VizieR Harvard mirror on 2026-11-11. Each Task 8 fixture is the builder's own query (same
source, columns and constraints) limited with `-out.max=50`, saved verbatim, plus the rows specific tests need.

Appended rows: each extra query's data rows were reordered to the base file's header and appended after a
`# Rows appended from: <url>` comment line (the TSV parser skips `#` lines). Rows already in the base file were not
duplicated, so some extra queries added 0 rows (their targets were already among the first 50).

Units to keep in mind: 2MRS `Riso` is log10 of the radius in arcsec; HyperLEDA `logD25` is log10 of D25 in
0.1 arcmin and `logR25` is log10(a/b); UNGC `a26` is in arcmin and `Dist` in Mpc; McConnachie `D` is in kpc and
`Ell` = 1 − b/a. UNGC, McConnachie, HyperLEDA, MCXC and Abell give sexagesimal RA/Dec, so their queries request
VizieR's computed decimal `_RAJ2000,_DEJ2000` columns instead. The Abell supplementary southern table
(`VII/110A/table5`) numbers its clusters in `ACOS`, not `ACO`.

## `cf4_sample.tsv` (SkyFramesTests)

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJ/944/94/table2&-out=PGC,RAJ2000,DEJ2000,SGL,SGB&-out.max=200" > cf4_sample.tsv
```

## Data-set builder fixtures (BuilderTests)

### `ungc.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/AJ/145/101/catalog&-out=Name,_RAJ2000,_DEJ2000,a26,b/a,TT,Tdw,HRV,Dist,i,Vlg&-out.max=50" > ungc.tsv
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/AJ/145/101/catalog&-out=Name,_RAJ2000,_DEJ2000,a26,b/a,TT,Tdw,HRV,Dist,i,Vlg&-c=10.6847%2B41.2690&-c.rm=1&-out.max=50"
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/AJ/145/101/catalog&-out=Name,_RAJ2000,_DEJ2000,a26,b/a,TT,Tdw,HRV,Dist,i,Vlg&-c=11.8880-25.2880&-c.rm=1&-out.max=50"
```

### `mcconnachie.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/AJ/144/4/catalog&-out=Name,MType,_RAJ2000,_DEJ2000,D,HRV,PA,Ell&-out.max=50" > mcconnachie.tsv
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/AJ/144/4/catalog&-out=Name,MType,_RAJ2000,_DEJ2000,D,HRV,PA,Ell&-c=10.6847%2B41.2690&-c.rm=1&-out.max=50"
```

### `hyperleda.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/237/pgc&-out=PGC,_RAJ2000,_DEJ2000,MType,logD25,logR25,PA&logD25=%3E0.6&-out.max=50" > hyperleda.tsv
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/237/pgc&-out=PGC,_RAJ2000,_DEJ2000,MType,logD25,logR25,PA&logD25=%3E0.6&-c=10.6847%2B41.2690&-c.rm=1&-out.max=50"
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/237/pgc&-out=PGC,_RAJ2000,_DEJ2000,MType,logD25,logR25,PA&logD25=%3E0.6&-c=187.7059%2B12.3911&-c.rm=1&-out.max=50"
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/237/pgc&-out=PGC,_RAJ2000,_DEJ2000,MType,logD25,logR25,PA&logD25=%3E0.6&-c=11.8880-25.2880&-c.rm=1&-out.max=50"
```

### `twomrs.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJS/199/26/table3&-out=ID,RAJ2000,DEJ2000,Kcmag,b/a,type,cz,Riso,CAT,SimbadName&-out.max=50" > twomrs.tsv
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJS/199/26/table3&-out=ID,RAJ2000,DEJ2000,Kcmag,b/a,type,cz,Riso,CAT,SimbadName&-c=10.6847%2B41.2690&-c.rm=1&-out.max=50"
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJS/199/26/table3&-out=ID,RAJ2000,DEJ2000,Kcmag,b/a,type,cz,Riso,CAT,SimbadName&-c=187.7059%2B12.3911&-c.rm=1&-out.max=50"
# then append its data rows (0 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJS/199/26/table3&-out=ID,RAJ2000,DEJ2000,Kcmag,b/a,type,cz,Riso,CAT,SimbadName&-c=11.8880-25.2880&-c.rm=1&-out.max=50"
```

### `cf4.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJ/944/94/table2&-out=PGC,RAJ2000,DEJ2000,DM,Vcmb&-out.max=50" > cf4.tsv
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJ/944/94/table2&-out=PGC,RAJ2000,DEJ2000,DM,Vcmb&-c=10.6847%2B41.2690&-c.rm=1&-out.max=50"
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJ/944/94/table2&-out=PGC,RAJ2000,DEJ2000,DM,Vcmb&-c=187.7059%2B12.3911&-c.rm=1&-out.max=50"
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/ApJ/944/94/table2&-out=PGC,RAJ2000,DEJ2000,DM,Vcmb&-c=11.8880-25.2880&-c.rm=1&-out.max=50"
```

### `mcxc.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/A%2BA/534/A109/mcxc&-out=MCXC,OName,AName,_RAJ2000,_DEJ2000,z,M500,R500,L500&-out.max=50" > mcxc.tsv
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/A%2BA/534/A109/mcxc&-out=MCXC,OName,AName,_RAJ2000,_DEJ2000,z,M500,R500,L500&-c=194.95%2B27.98&-c.rm=10&-out.max=50"
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=J/A%2BA/534/A109/mcxc&-out=MCXC,OName,AName,_RAJ2000,_DEJ2000,z,M500,R500,L500&-c=187.70%2B12.39&-c.rm=30&-out.max=50"
```

### `abell3.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/110A/table3&-out=ACO,_RAJ2000,_DEJ2000,z,Rich,BMtype&z=%3E0&-out.max=50" > abell3.tsv
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/110A/table3&-out=ACO,_RAJ2000,_DEJ2000,z,Rich,BMtype&z=%3E0&-c=194.95%2B27.98&-c.rm=10&-out.max=50"
```

### `abell4.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/110A/table4&-out=ACO,_RAJ2000,_DEJ2000,z,Rich,BMtype&z=%3E0&-out.max=50" > abell4.tsv
```

### `abell5.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/110A/table5&-out=ACOS,_RAJ2000,_DEJ2000,z,Rich,BMtype&z=%3E0&-out.max=50" > abell5.tsv
```

### `milliquas.tsv`

```sh
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/294/catalog&-out=RAJ2000,DEJ2000,Name,Type,Rmag,Bmag,z&z=%3E0&-out.max=50" > milliquas.tsv
# then append its data rows (1 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/294/catalog&-out=RAJ2000,DEJ2000,Name,Type,Rmag,Bmag,z&z=%3E0&-c=187.2779%2B2.0524&-c.rm=1&-out.max=50"
# then append its data rows (3 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/294/catalog&-out=RAJ2000,DEJ2000,Name,Type,Rmag,Bmag,z&z=%3E0&Type=R&-out.max=3"
# then append its data rows (3 new):
curl -s "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv?-source=VII/294/catalog&-out=RAJ2000,DEJ2000,Name,Type,Rmag,Bmag,z&z=%3E0&Type=X&-out.max=3"
```

### Hand edit

`milliquas.tsv` ends with one synthetic row (marked by a comment) whose RA is blank, for
`Builder_CountsSkippedRows`. It is not VizieR data.
