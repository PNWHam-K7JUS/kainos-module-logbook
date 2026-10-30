/*  CtyBuiltIn.cs

A short list in cty.dat's format: the common prefixes, so lookups work before cty.dat has been downloaded.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

namespace Kainos.Logbook
{
    internal static class BuiltIn
    {
        public const string Text = @"
United States:              5:   8:  NA:    39.00:    98.00:   7.0:  K:
    K,W,N,AA,AB,AC,AD,AE,AF,AG,AI,AJ,AK;
Alaska:                     1:   1:  NA:    61.20:   149.90:  10.0:  KL:
    KL,AL,NL,WL;
Hawaii:                    31:  61:  OC:    21.30:   157.80:  11.0:  KH6:
    KH6,KH7,AH6,NH6,WH6;
Puerto Rico:                8:  11:  NA:    18.40:    66.10:   4.0:  KP4:
    KP4,NP4,WP4,KP3;
US Virgin Islands:          8:  11:  NA:    18.30:    64.90:   4.0:  KP2:
    KP2,NP2,WP2;
Guam:                      27:  64:  OC:    13.50:  -144.80: -10.0:  KH2:
    KH2,AH2,NH2,WH2;
Canada:                     4:   4:  NA:    45.40:    75.70:   5.0:  VE:
    VE,VA,VO,VY,CY,CF,CG,CJ,CK;
Mexico:                     6:  10:  NA:    19.40:    99.10:   7.0:  XE:
    XE,XF,4A,4B,4C;
England:                   14:  27:  EU:    52.00:     1.50:   0.0:  G:
    G,M,2E;
Wales:                     14:  27:  EU:    52.30:     3.60:   0.0:  GW:
    GW,MW,2W;
Scotland:                  14:  27:  EU:    56.50:     4.00:   0.0:  GM:
    GM,MM,2M;
Northern Ireland:          14:  27:  EU:    54.60:     6.00:   0.0:  GI:
    GI,MI,2I;
Isle of Man:               14:  27:  EU:    54.20:     4.50:   0.0:  GD:
    GD,MD;
Jersey:                    14:  27:  EU:    49.20:     2.10:   0.0:  GJ:
    GJ,MJ;
Guernsey:                  14:  27:  EU:    49.50:     2.60:   0.0:  GU:
    GU,MU;
Ireland:                   14:  27:  EU:    53.30:     6.30:   0.0:  EI:
    EI,EJ;
France:                    14:  27:  EU:    46.50:    -2.50:   0.0:  F:
    F;
Germany:                   14:  28:  EU:    51.00:   -10.00:  -1.0:  DA:
    DA,DB,DC,DD,DE,DF,DG,DH,DJ,DK,DL,DM,DN,DO,DP,DQ,DR;
Belgium:                   14:  27:  EU:    50.80:    -4.40:   0.0:  ON:
    ON,OO,OP,OQ,OR,OS,OT;
Netherlands:               14:  27:  EU:    52.20:    -5.30:   0.0:  PA:
    PA,PB,PC,PD,PE,PF,PG,PH,PI;
Luxembourg:                14:  27:  EU:    49.60:    -6.10:   0.0:  LX:
    LX;
Switzerland:               14:  28:  EU:    46.80:    -8.20:  -1.0:  HB:
    HB;
Liechtenstein:             14:  28:  EU:    47.10:    -9.50:  -1.0:  HB0:
    HB0;
Austria:                   15:  28:  EU:    48.20:   -16.40:  -1.0:  OE:
    OE;
Italy:                     15:  28:  EU:    42.00:   -12.50:  -1.0:  I:
    I,IK,IZ,IU,IW,IQ;
Sardinia:                  15:  28:  EU:    40.00:    -9.00:  -1.0:  IS0:
    IS0,IM0;
Spain:                     14:  37:  EU:    40.40:     3.70:   0.0:  EA:
    EA,EB,EC,ED,EE,EF,EG,EH;
Balearic Islands:          14:  37:  EU:    39.60:    -2.90:   0.0:  EA6:
    EA6,EB6,EC6;
Canary Islands:            33:  36:  AF:    28.10:    15.40:   1.0:  EA8:
    EA8,EB8,EC8;
Portugal:                  14:  37:  EU:    38.70:     9.10:   1.0:  CT:
    CT,CQ,CS;
Azores:                    14:  36:  EU:    37.70:    25.70:   2.0:  CU:
    CU;
Madeira:                   33:  36:  AF:    32.60:    16.90:   1.0:  CT3:
    CT3,CQ3;
Denmark:                   14:  18:  EU:    55.70:   -12.60:  -1.0:  OZ:
    OZ,OU,OV;
Faroe Islands:             14:  18:  EU:    62.00:     6.80:   0.0:  OY:
    OY;
Norway:                    14:  18:  EU:    60.00:   -10.70:  -1.0:  LA:
    LA,LB,LC,LD,LE,LF,LG,LH,LI,LJ,LK,LL,LM,LN;
Sweden:                    14:  18:  EU:    59.30:   -18.10:  -1.0:  SM:
    SM,SA,SB,SC,SD,SE,SF,SG,SH,SI,SJ,SK,SL,7S,8S;
Finland:                   15:  18:  EU:    60.20:   -24.90:  -2.0:  OH:
    OH,OF,OG,OI,OJ;
Aland Islands:             15:  18:  EU:    60.10:   -19.90:  -1.0:  OH0:
    OH0;
Estonia:                   15:  29:  EU:    59.40:   -24.70:  -2.0:  ES:
    ES;
Latvia:                    15:  29:  EU:    56.90:   -24.10:  -2.0:  YL:
    YL;
Lithuania:                 15:  29:  EU:    54.70:   -25.30:  -2.0:  LY:
    LY;
Poland:                    15:  28:  EU:    52.20:   -21.00:  -1.0:  SP:
    SP,SN,SO,SQ,SR,3Z,HF;
Czech Republic:            15:  28:  EU:    50.10:   -14.40:  -1.0:  OK:
    OK,OL;
Slovak Republic:           15:  28:  EU:    48.10:   -17.10:  -1.0:  OM:
    OM;
Hungary:                   15:  28:  EU:    47.50:   -19.00:  -1.0:  HA:
    HA,HG;
Romania:                   20:  28:  EU:    44.40:   -26.10:  -2.0:  YO:
    YO,YP,YQ,YR;
Bulgaria:                  20:  28:  EU:    42.70:   -23.30:  -2.0:  LZ:
    LZ;
Greece:                    20:  28:  EU:    38.00:   -23.70:  -2.0:  SV:
    SV,SW,SX,SY,SZ,J4;
Crete:                     20:  28:  EU:    35.30:   -25.10:  -2.0:  SV9:
    SV9,SW9,SX9,J49;
Croatia:                   15:  28:  EU:    45.80:   -16.00:  -1.0:  9A:
    9A;
Slovenia:                  15:  28:  EU:    46.00:   -14.50:  -1.0:  S5:
    S5;
Serbia:                    15:  28:  EU:    44.80:   -20.50:  -1.0:  YT:
    YT,YU;
Bosnia-Herzegovina:        15:  28:  EU:    43.90:   -18.40:  -1.0:  E7:
    E7;
North Macedonia:           15:  28:  EU:    42.00:   -21.40:  -1.0:  Z3:
    Z3;
Albania:                   15:  28:  EU:    41.30:   -19.80:  -1.0:  ZA:
    ZA;
Montenegro:                15:  28:  EU:    42.40:   -19.30:  -1.0:  4O:
    4O;
Malta:                     15:  28:  EU:    35.90:   -14.50:  -1.0:  9H:
    9H;
European Russia:           16:  29:  EU:    55.75:   -37.60:  -3.0:  UA:
    UA,UB,UC,UD,UE,UF,UG,UH,UI,R,RA,RB,RC,RD,RE,RF,RG,RH,RI,RJ,RK,RL,RM,RN,RO,RP,RQ,RT,RU,RV,RW,RX,RY,RZ;
Asiatic Russia:            17:  30:  AS:    55.00:   -83.00:  -6.0:  UA9:
    UA9,UA0,RA9,RA0,R9,R0,RU9,RV9,RW9,RX9,RZ9,UA8,R8;
Kaliningrad:               15:  29:  EU:    54.70:   -20.50:  -1.0:  UA2:
    UA2,RA2,R2F,R2K;
Ukraine:                   16:  29:  EU:    50.45:   -30.50:  -2.0:  UR:
    UR,US,UT,UU,UV,UW,UX,UY,UZ,EM,EN,EO;
Belarus:                   16:  29:  EU:    53.90:   -27.60:  -2.0:  EU:
    EU,EV,EW;
Moldova:                   16:  29:  EU:    47.00:   -28.80:  -2.0:  ER:
    ER;
Israel:                    20:  39:  AS:    32.00:   -34.80:  -2.0:  4X:
    4X,4Z;
Asiatic Turkey:            20:  39:  AS:    39.90:   -32.90:  -2.0:  TA:
    TA,TB,TC,YM;
European Turkey:           20:  39:  EU:    41.00:   -28.90:  -2.0:  TA1:
    TA1;
Oman:                      21:  39:  AS:    23.60:   -58.50:  -4.0:  A4:
    A4;
United Arab Emirates:      21:  39:  AS:    24.50:   -54.40:  -4.0:  A6:
    A6;
Qatar:                     21:  39:  AS:    25.30:   -51.50:  -3.0:  A7:
    A7;
Saudi Arabia:              21:  39:  AS:    24.70:   -46.70:  -3.0:  HZ:
    HZ,7Z,8Z;
Georgia:                   21:  29:  AS:    41.70:   -44.80:  -3.0:  4L:
    4L;
Armenia:                   21:  29:  AS:    40.20:   -44.50:  -3.0:  EK:
    EK;
Azerbaijan:                21:  29:  AS:    40.40:   -49.90:  -3.0:  4J:
    4J,4K;
Kazakhstan:                17:  30:  AS:    51.20:   -71.40:  -5.0:  UN:
    UN,UO,UP,UQ;
Pakistan:                  21:  41:  AS:    33.70:   -73.00:  -5.0:  AP:
    AP,AS;
India:                     22:  41:  AS:    28.60:   -77.20:  -5.0:  VU:
    VU,AT,AU,AV,AW;
Sri Lanka:                 22:  41:  AS:     6.90:   -79.90:  -5.0:  4S:
    4S;
Nepal:                     22:  42:  AS:    27.70:   -85.30:  -6.0:  9N:
    9N;
Japan:                     25:  45:  AS:    35.70:  -139.70:  -9.0:  JA:
    JA,JE,JF,JG,JH,JI,JJ,JK,JL,JM,JN,JO,JP,JQ,JR,JS,7J,7K,7L,7M,7N,8J,8N;
South Korea:               25:  44:  AS:    37.50:  -127.00:  -8.0:  HL:
    HL,DS,6K,6L,6M,6N;
China:                     24:  44:  AS:    39.90:  -116.40:  -8.0:  BY:
    BY,BA,BD,BG,BH,BI,BJ,BL;
Taiwan:                    24:  44:  AS:    25.00:  -121.50:  -8.0:  BV:
    BV,BM,BN,BO,BP,BQ,BU,BW,BX;
Hong Kong:                 24:  44:  AS:    22.30:  -114.20:  -8.0:  VR2:
    VR2;
Thailand:                  26:  49:  AS:    13.80:  -100.50:  -7.0:  HS:
    HS,E2;
Singapore:                 28:  54:  AS:     1.35:  -103.80:  -7.0:  9V:
    9V;
West Malaysia:             28:  54:  AS:     3.10:  -101.70:  -7.0:  9M2:
    9M2,9W2;
Indonesia:                 28:  54:  OC:    -6.20:  -106.80:  -7.0:  YB:
    YB,YC,YD,YE,YF,YG,YH;
Philippines:               27:  50:  OC:    14.60:  -121.00:  -8.0:  DU:
    DU,DV,DW,DX,DY,DZ,4D,4E,4F,4G,4H,4I;
Vietnam:                   26:  49:  AS:    21.00:  -105.80:  -7.0:  XV:
    XV,3W;
Australia:                 30:  59:  OC:   -33.90:  -151.20: -10.0:  VK:
    VK,AX;
New Zealand:               32:  60:  OC:   -41.30:  -174.80: -12.0:  ZL:
    ZL,ZM;
New Caledonia:             32:  56:  OC:   -22.30:  -166.50: -11.0:  FK:
    FK;
French Polynesia:          32:  63:  OC:   -17.50:   149.60:  10.0:  FO:
    FO;
Fiji:                      32:  56:  OC:   -18.10:  -178.40: -12.0:  3D2:
    3D2;
Samoa:                     32:  62:  OC:   -13.80:   171.80:  11.0:  5W:
    5W;
Papua New Guinea:          28:  51:  OC:    -9.40:  -147.20: -10.0:  P2:
    P2;
Brazil:                    11:  15:  SA:   -23.50:    46.60:   3.0:  PY:
    PY,PP,PQ,PR,PS,PT,PU,PV,PW,PX,ZV,ZW,ZX,ZY,ZZ;
Argentina:                 13:  14:  SA:   -34.60:    58.40:   4.0:  LU:
    LU,LO,LP,LQ,LR,LS,LT,LV,LW,AY,AZ,L2,L3,L4,L5,L6,L7,L8,L9;
Chile:                     12:  14:  SA:   -33.40:    70.60:   5.0:  CE:
    CE,CA,CB,CC,CD,XQ,XR,3G;
Uruguay:                   13:  14:  SA:   -34.90:    56.20:   4.0:  CX:
    CX,CV,CW;
Paraguay:                  11:  14:  SA:   -25.30:    57.60:   4.0:  ZP:
    ZP;
Peru:                      10:  12:  SA:   -12.00:    77.00:   5.0:  OA:
    OA,OB,OC,4T;
Colombia:                   9:  12:  SA:     4.70:    74.00:   5.0:  HK:
    HK,HJ,5J,5K;
Venezuela:                  9:  12:  SA:    10.50:    66.90:   4.0:  YV:
    YV,YW,YX,YY,4M;
Ecuador:                   10:  12:  SA:    -0.20:    78.50:   5.0:  HC:
    HC,HD;
Bolivia:                   10:  12:  SA:   -16.50:    68.10:   5.0:  CP:
    CP;
Costa Rica:                 7:  11:  NA:     9.90:    84.10:   6.0:  TI:
    TI,TE;
Panama:                     7:  11:  NA:     9.00:    79.50:   5.0:  HP:
    HP,HO,3E,3F;
Guatemala:                  7:  11:  NA:    14.60:    90.50:   6.0:  TG:
    TG,TD;
Cuba:                       8:  11:  NA:    23.10:    82.40:   5.0:  CO:
    CO,CM,CL,T4;
Dominican Republic:         8:  11:  NA:    18.50:    69.90:   5.0:  HI:
    HI;
Jamaica:                    8:  11:  NA:    18.00:    76.80:   5.0:  6Y:
    6Y;
Bahamas:                    8:  11:  NA:    25.00:    77.30:   5.0:  C6:
    C6;
Bermuda:                    5:  11:  NA:    32.30:    64.80:   4.0:  VP9:
    VP9;
Curacao:                    9:  11:  SA:    12.10:    68.90:   5.0:  PJ2:
    PJ2;
Aruba:                      9:  11:  SA:    12.50:    70.00:   5.0:  P4:
    P4;
Martinique:                 8:  11:  NA:    14.60:    61.00:   4.0:  FM:
    FM;
Barbados:                   8:  11:  NA:    13.10:    59.60:   4.0:  8P:
    8P;
Falkland Islands:          13:  16:  SA:   -51.70:    57.90:   4.0:  VP8:
    VP8;
South Africa:              38:  57:  AF:   -26.20:   -28.00:  -2.0:  ZS:
    ZS,ZR,ZT,ZU;
Kenya:                     37:  48:  AF:    -1.30:   -36.80:  -2.0:  5Z:
    5Z;
Tanzania:                  37:  53:  AF:    -6.80:   -39.30:  -3.0:  5H:
    5H;
Zambia:                    36:  53:  AF:   -15.40:   -28.30:  -2.0:  9J:
    9J;
Morocco:                   33:  37:  AF:    34.00:     6.80:   0.0:  CN:
    CN,5C,5D;
Algeria:                   33:  37:  AF:    36.80:    -3.10:   0.0:  7X:
    7X;
Egypt:                     34:  38:  AF:    30.00:   -31.20:  -2.0:  SU:
    SU;
Nigeria:                   35:  46:  AF:     9.10:    -7.40:   0.0:  5N:
    5N;
Ghana:                     35:  46:  AF:     5.60:     0.20:   0.0:  9G:
    9G;
Senegal:                   35:  46:  AF:    14.70:    17.40:   1.0:  6W:
    6W;
Cape Verde:                35:  46:  AF:    14.90:    23.50:   2.0:  D4:
    D4;
Mauritius:                 39:  53:  AF:   -20.20:   -57.50:  -4.0:  3B8:
    3B8;
Reunion:                   39:  53:  AF:   -20.90:   -55.50:  -4.0:  FR:
    FR;
Madagascar:                39:  53:  AF:   -18.90:   -47.50:  -3.0:  5R:
    5R;
Iceland:                   40:  17:  EU:    64.10:    21.90:   1.0:  TF:
    TF;
Greenland:                 40:   5:  NA:    64.20:    51.70:   3.0:  OX:
    OX;
Cyprus:                    20:  39:  AS:    35.20:   -33.40:  -2.0:  5B:
    5B,C4,H2,P3;
Gibraltar:                 14:  37:  EU:    36.10:     5.40:   0.0:  ZB2:
    ZB2;
Andorra:                   14:  27:  EU:    42.50:    -1.50:   0.0:  C3:
    C3;
Monaco:                    14:  27:  EU:    43.70:    -7.40:   0.0:  3A:
    3A;
San Marino:                15:  28:  EU:    43.90:   -12.40:  -1.0:  T7:
    T7;
";
    }
}
