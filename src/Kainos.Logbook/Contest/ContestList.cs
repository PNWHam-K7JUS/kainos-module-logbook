/*  ContestList.cs

The contests the logbook knows: id | name | when | modes (CW, PH phone, RY RTTY, DG digital) | the exchange
received | the exchange sent (if different) | scoring | dupes (band, bandmode, mode, once) | Cabrillo CONTEST: |
a note.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

namespace Kainos.Logbook.Contest
{
    internal static class ContestList
    {
        public const string Text = @"
cqwwssb|CQ World Wide DX SSB|Last full weekend of October|PH|rst zone||cqww|band|CQ-WW-SSB|
cqwwcw|CQ World Wide DX CW|Last full weekend of November|CW|rst zone||cqww|band|CQ-WW-CW|
cqwwrtty|CQ WW RTTY DX|Last full weekend of September|RY|rst zone state||cqww|band|CQ-WW-RTTY|W/VE also send state or province
cqwpxssb|CQ WPX SSB|Last full weekend of March|PH|rst serial||wpx|band|CQ-WPX-SSB|
cqwpxcw|CQ WPX CW|Last full weekend of May|CW|rst serial||wpx|band|CQ-WPX-CW|
cqwpxrtty|CQ WPX RTTY|Second full weekend of February|RY|rst serial||wpx|band|CQ-WPX-RTTY|
arrldxcw|ARRL International DX CW|Third full weekend of February|CW|rst pwr|rst state|arrldx|band|ARRL-DX-CW|You send RST + WA; DX send RST + power. W/VE don't work each other.
arrldxssb|ARRL International DX SSB|First full weekend of March|PH|rst pwr|rst state|arrldx|band|ARRL-DX-SSB|You send RST + WA; DX send RST + power. W/VE don't work each other.
sscw|ARRL November Sweepstakes CW|First full weekend of November|CW|serial prec chk sec|serial prec call chk sec|ss|once|ARRL-SS-CW|
ssssb|ARRL November Sweepstakes SSB|Third full weekend of November|PH|serial prec chk sec|serial prec call chk sec|ss|once|ARRL-SS-SSB|
fd|ARRL Field Day|Fourth full weekend of June|PH CW DG|cls sec||fd|bandmode|ARRL-FD|
wfd|Winter Field Day|Last full weekend of January|PH CW DG|cls sec||fd|bandmode|WFD|
arrl10|ARRL 10 Meter|Second full weekend of December|PH CW|rst state||arrl10|mode|ARRL-10|DX send RST + serial instead of a state
arrl160|ARRL 160 Meter|First full weekend of December|CW|rst sec||arrl160|band|ARRL-160|DX send RST only
rttyru|ARRL RTTY Roundup|First full weekend of January|RY DG|rst state||rttyru|bandmode|ARRL-RTTY|DX send RST + serial
arrldigi|ARRL International Digital (FT4/FT8)|April|DG|grid||dist500|band|ARRL-DIGI|
cqwwdigi|CQ WW Digi (FT4/FT8)|Last full weekend of June|DG|grid||dist3000|bandmode|CQ-WW-DIGI|
naqpcw|North American QSO Party CW|January and August|CW|name state||naqp|band|NAQP-CW|
naqpssb|North American QSO Party SSB|January and August|PH|name state||naqp|band|NAQP-SSB|
naqprtty|North American QSO Party RTTY|February and July|RY|name state||naqp|band|NAQP-RTTY|
sprint|North American Sprint (CW / SSB)|February and September|CW PH|serial name state||sprint|band|NA-SPRINT-CW|After calling CQ and working someone, you must QSY
iaru|IARU HF World Championship|Second full weekend of July|PH CW|rst itu||iaru|bandmode|IARU-HF|
cq160|CQ 160 Meter (CW / SSB)|Last full weekends of January (CW) and February (SSB)|CW PH|rst state||cq160|once|CQ-160-CW|DX send RST + CQ prefix
vhfjun|ARRL June VHF|Second full weekend of June|PH CW DG|grid||vhf|band|ARRL-VHF-JUN|
vhfsep|ARRL September VHF|Second full weekend of September|PH CW DG|grid||vhf|band|ARRL-VHF-SEP|
vhfjan|ARRL January VHF|Third full weekend of January|PH CW DG|grid||vhf|band|ARRL-VHF-JAN|
stew|Stew Perry Topband Challenge|December and April|CW|grid||dist500|once|STEW-PERRY|
cwt|CWops CWT (weekly)|Wednesdays 1300, 1900, 0300 and Thursday 0700 UTC, one hour|CW|name num|name num|cwt|band|CWOPS-CWT|Non-members send state, province or country instead of a number
sst|K1USN Slow Speed Test (weekly)|Fridays 2000 and Mondays 0000 UTC|CW|name state||sst|band|K1USN-SST|
wapsalmon|Washington State Salmon Run|Third full weekend of September|PH CW DG|rst loc|rst county|qp|bandmode|WA-STATE-QSO-PARTY|State QSO party: in-state send county, others state/prov/DX. Kainos would carry the county lists for every state party.
wae|Worked All Europe DX|August CW, September SSB, November RTTY|CW PH RY|rst serial||wae|band|DARC-WAEDC-CW|Outside Europe, work Europe only. QTC traffic is not in the mockup.
aadx|All Asian DX (CW / SSB)|June CW, September SSB|CW PH|rst age|rst age|aadx|band|AP-SPRINT|Outside Asia, work Asia only
jidx|JIDX (CW / SSB)|April CW, November SSB|CW PH|rst zone||jidx|band|JIDX-CW|Outside Japan, work Japan only; JA send their prefecture
rdxc|Russian DX Contest|Third full weekend of March|CW PH|rst serial||generic|bandmode|RDXC|UA stations send their oblast
racwin|RAC Winter Contest|Last Saturday of December|CW PH|rst serial||generic|bandmode|RAC-WINTER|VE stations send their province
euhf|European HF Championship|First Saturday of August|CW PH|rst yr|rst yr|generic|bandmode|EU-HF|Exchange is the year you were first licensed
okom|OK/OM DX|November CW, December SSB|CW PH|rst serial||generic|band|OK-OM-DX|
";
    }
}
