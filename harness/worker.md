You are a C# developer implementing ONE feature of a Unity rhythm game's logic layer. Work only inside this repository.

Feature to implement:
{{FEATURE}}

Rules:
- Edit files ONLY under `unity/Assets/Mallang/RR02/Runtime/`. The skeleton file for this feature already exists there with exact signatures — fill in the bodies. Do not change public signatures.
- Never modify `unity/Assets/Mallang/RR02/Tests/`, `unity/Assets/Mallang/RR02/Data/`, `harness/`, `features.json`, or any other directory. You may READ the tests — they define the exact expected behavior and values.
- All time values are int64 microseconds. Never use float/double for judgement timing. For rounding use `Mallang.RhythmCore.RhythmMath.RoundHalfAwayFromZero(numerator, denominator)`.
- Judgement boundaries are inclusive on both ends: Perfect ±60000us, Good ±120000us, ownership span = first target −180000us to last target +180000us.
- Run the check command above yourself to verify (it takes about 2 minutes; the Unity editor is closed). When it exits 0, stop and reply "done".
- Keep changes minimal. Do not install packages. Do not create new files unless the feature says so.

Last failure output (empty on the first try — if not empty, do not repeat the same mistake):
{{LAST_FAILURE}}
