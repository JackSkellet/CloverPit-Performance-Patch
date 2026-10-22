# Validation and limitations

Target: Steam app **3314790**, build **22785177**, Unity **6000.2.6f2**.

The results below concern the algorithms and runtime helper incorporated from the earlier two patch revisions. They distinguish controlled correctness checks from actual gameplay observations. They are not an FPS benchmark.

## Data caching and transition speed

Executed the original and patched data-validation methods against fresh and warmed arrays, replacement arrays, reversed records, oversized arrays, and missing slots. Live value changes remained visible.

For **1,000 warmed powerup lookups**, the original performed **205,000 enum-to-string conversions**; the patch performed **zero**. The corresponding symbol and pattern cases changed from 9,000 and 16,000 conversions to zero. These are work counts, not measured frame-rate gains.

Executed the actual patched menu and console methods in an isolated harness with UI, audio, and settings-application calls stubbed. Full forward/backward speed cycles, edge cases, and the menu's save flag passed. The user also confirmed that 64× appeared in the real game and an ordinary spin completed.

## Pattern ordering

Compared exact pattern-record reference order across **61,087 C# differential cases**, including ties, disabled entries, negative and arbitrarily large scores, and repeated calls after object mutation.

In a retained pool of **4,096 records with 32 active entries**, order-weight calls fell from **16,773,120 to 32**, while the complete record order matched the original. The test used a stub pattern type to isolate algorithm behavior; the patched scoring coroutine also passed JIT validation.

Active-only exchange sorting remains quadratic. Pattern containment and insertion scans were not removed. There is no claim that every possible scoring bottleneck has been eliminated.

## Text-opacity update

- **57 offline cases and 840 assertions** passed against the production helper source using synthetic Unity/TMP fixtures.
- A test in the **actual Unity/TMP runtime** passed **8 alpha cases**, including recovery from zero opacity, with identical rendered RGBA bytes, vertex positions, and UVs.
- Runtime counters confirmed that the fast path was used. Complex or uncertain text also exercised the original-setter fallback.
- The user confirmed that the effects looked unchanged.

The original CRT logic retains control over waves, flicker, pauses, and reduced flashing. The optimization applies only to eligible plain text. Tags, gradients, sprites, decorations, custom callbacks, uncertain mesh states, and other unsupported cases use the original behavior.

## Hidden carousel

**5,280 interpolation cases** passed. Accumulating hidden interpolation and applying it once differed from repeating the single-precision steps by at most approximately **0.00001145 per scalar component** in those tests. This is not a claim of bit-identical transforms or a pixel-error bound.

Structural checks preserved behavior outside the hidden carousel block and placed catch-up operations before both reopening paths. The user checked reopening in the real game and reported normal appearance.

## Assembly and installation checks

REA parsed the patched assembly with complete metadata coverage and no issues. Regression checks retained the earlier data-cache and transition-speed behavior after the opacity/carousel changes.

The earlier installers completed isolated install/restore roundtrips: the first restored the exact original assembly; the second restored the exact first-patch files. Both preserved newer save progress and `boot.config`. These results do not, by themselves, validate a newly packaged combined installer.

The **combined self-contained Linux CLI** also passed a separate local roundtrip against the supported original game assembly:

- Installing from the clean original produced the exact first-stage assembly and then the exact final-v2 assembly already tested in game. The companion assembly also matched the installed final-v2 helper.
- `verify` passed, and installing again recognized the final patch without changing it.
- `restore` returned the exact original assembly and removed the patch's own companion assembly.
- Save progress changed after installation was preserved during restore; a `boot.config` sentinel remained unchanged.
- An unknown modified game assembly was rejected without changing it.

SHA-256 identities used in that comparison:

| File/stage | SHA-256 |
| --- | --- |
| Original game assembly | `7e904219b9c26bb58efd9170d13dfb5070bd848add6d2d117223910284a7498e` |
| First-stage patched assembly | `012252f4ac421044fc5c98013237fa4ca5a6fccf76c3487b9c4a343b0dfb34df` |
| Final-v2 game assembly | `8508cde02362bcc9ffe566a6b5fca9457e8f53d284f91c570e5c95a3f034040a` |
| Final-v2 companion assembly | `4e10da52074c8f5e0e7293e297365816b1cc1dbd1baab3659fcafab45302e283` |

Both Windows x64 and Linux x64 passed the public algorithm suite, self-contained build, and CLI smoke checks in [GitHub Actions](https://github.com/JackSkellet/CloverPit-Performance-Patch/actions/runs/39706304311). The downloaded Linux CI artifact also passed install, verify, and exact-original restoration against a local game copy. **Windows gameplay remains untested.** A successful installer or algorithm test is not a Windows gameplay test.

## What the evidence supports

The code avoids verified redundant work, the tested scoring order is preserved, the native opacity comparison passed, and the user reported smoother play with the original symptom no longer reproducible.

There is **no controlled before/after FPS benchmark**. The earlier intermittent high-score event was not captured under matched conditions. The results do not prove that every hitch is fixed or predict a particular speedup on another computer.
