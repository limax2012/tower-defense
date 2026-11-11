; Offline material voices. p4: impact flag, p5: stage (0..3), p6: variant (0..2).
sr = 44100
ksmps = 1
nchnls = 1
0dbfs = 1

giSine ftgen 1, 0, 16384, 10, 1
giGrainRise ftgen 2, 0, 4096, 19, 0.5, 0.5, 270, 0.5

; A compact cloud of overlapping plasma grains follows the fan's single launch.
instr 1
    iImpact = p4
    iStage = p5
    iVariant = p6 - 1
    iSeed = 0.271 + p6 * 0.047 + iStage * 0.011 + iImpact * 0.119
    iColor = 1 + iVariant * 0.045
    kEnvelope transeg 0, 0.003, 0, 1, p3 * 0.26, -2, 0.68, p3 * 0.74 - 0.003, -3, 0
    kGrainRate randi 34, 137, iSeed, 1
    kFormant line (1160 - iStage * 42) * iColor, p3, (990 - iStage * 28) * iColor
    aGrains fof 0.48, 145 + kGrainRate, kFormant, 0, 640 + iImpact * 190, 0.0015, 0.012, 0.009, 30, giSine, giGrainRise, p3
    aNoise rand 1, iSeed, 1
    aLowCloud reson aNoise, (710 - iStage * 24) * iColor, 580 + iImpact * 130, 2
    aHighCloud reson aNoise, (1870 - iStage * 30) * iColor, 1100, 2
    aCore reson aNoise, (420 - iStage * 18) * iColor, 360, 2
    kCloudEnvelope transeg 0, 0.002, 0, 1, p3 - 0.002, -4, 0
    aPlasma = aGrains * (0.88 - iImpact * 0.23) * kEnvelope + (aLowCloud * 0.21 + aHighCloud * 0.075 + aCore * (0.035 + iStage * 0.017)) * kCloudEnvelope
    aClean butterhp aPlasma, 95
    aRound butterlp aClean, 4400 - iImpact * 500
    out aRound
endin

; Ignition excites a broad thermal cavity; the energized core stays nearly steady.
instr 2
    iImpact = p4
    iStage = p5
    iVariant = p6 - 1
    iSeed = 0.193 + p6 * 0.063 + iStage * 0.017 + iImpact * 0.073
    iColor = 1 + iVariant * 0.04
    aNoise rand 1, iSeed, 1
    aPressure reson aNoise, (490 + iStage * 24) * iColor, 660, 2
    aHeat reson aNoise, (1320 + iStage * 40) * iColor, 1450, 2
    aTurbulence butterlp aNoise, 72
    aCore poscil 0.20 + iStage * 0.017, (258 + iStage * 9) * iColor + aTurbulence * 170, giSine
    aHalo poscil 0.025 + iStage * 0.008, (781 + iStage * 13) * iColor + aTurbulence * 240, giSine
    kIgnition transeg 0, 0.004, 0, 1, 0.027, -2, 0.52, p3 - 0.031, -4, 0
    kPressure transeg 0, 0.009, 0, 1, p3 * 0.48, -1, 0.48, p3 * 0.52 - 0.009, -3, 0
    kCore transeg 0, 0.005, 0, 1, p3 - 0.005, -3, 0
    aSparkEnvelope linseg 0, 0.018 + p6 * 0.002, 0, 0.0005, 1, 0.0015, 0, 0.024, 0, 0.0005, 0.65, 0.0015, 0, p3 - 0.046 - p6 * 0.002, 0
    aSparks reson aNoise * aSparkEnvelope, 2680, 1800, 2
    aThermal = aPressure * (0.25 + iImpact * 0.055) * kPressure + aHeat * 0.105 * kIgnition + (aCore + aHalo) * kCore + aSparks * (0.022 + iStage * 0.004)
    aClean butterhp aThermal, 90
    aWarm butterlp aClean, 3900
    out aWarm
endin

; A quick coherent coolant pulse carries quiet, rapidly damped crystal modes.
instr 3
    iImpact = p4
    iStage = p5
    iVariant = p6 - 1
    iColor = 1 + iVariant * 0.012
    kPitch expseg 610 * iColor, 0.055, 360 * iColor, p3 - 0.055, 360 * iColor
    aBody poscil 1 - iImpact * 0.20, kPitch, giSine
    kBodyEnvelope transeg 0, 0.0018, 0, 1, 0.0532, -2, 0, p3 - 0.055, 0, 0
    aExcite mpulse 1, 0
    aIceA mode aExcite, (1520 + iImpact * 170) * iColor, 72 + iStage * 5
    aIceB mode aExcite, (2273 + iImpact * 120) * iColor, 81 + iStage * 4
    aIceC mode aExcite, (3197 + iImpact * 160) * iColor, 83
    aDepth mode aExcite, 843 * iColor, 37
    aApex mode aExcite, 1831 * iColor, 56
    kIceEnvelope transeg 1, p3, -3, 0
    aIce = (aIceA * 0.35 + aIceB * 0.23 + aIceC * 0.12) * (1 + iStage * 0.13 + iImpact * 0.32) + aDepth * iStage * 0.055 + aApex * max(0, iStage - 2) * 0.08
    aCoolant = aBody * kBodyEnvelope + aIce * kIceEnvelope
    aClean butterhp aCoolant, 105
    aGlass butterlp aClean, 5300
    out aGlass
endin
