# Algorithm regression plan

1. **Scaffold**: pure net10.0 MSTest project, Core ProjectReference, centrally pinned packages, class/method-isolated parallel tests, explicit Core-only Cobertura coverage settings.
2. **Signal processing**: cadence steady rates, stop timeout, window eviction, reset, invalid ordering; peak transitions and refractory period; accelerometer 90/120/180/210/240 SPM at 25/50/100 Hz, stationary noise, orientation, irregular sampling, restart, invalid samples.
3. **GPS**: Haversine reference distances and speed conversions; filter valid bounds, precision, speed, time order and reset; tracker stale/future locations, total/average/current speed, noise circles, native speed, pause/gap segmentation, reset, invalid-state recovery.
4. **Verify**: compile and run with unique MSBuild binlog, collect Cobertura, review meaningful assertions and mutation-sensitive edge cases. Target >=80% Core line coverage without weakening failed expectations. Report production failures to main task.

Success requires deterministic passing tests against actual Core source. Full Android application build, installation, and physical sensor validation are separate main-task responsibilities.

## Completed

All four phases complete. Added hardware cumulative step accounting, batched cadence, interleaved timer/callback tests, and monotonic sensor-clock integration regressions as the shared APIs were introduced. Final result: 168/168 passing test cases, 100% Core line coverage and 98.35% branch coverage; no production failures remain in this suite. The interleaving follow-up first demonstrated five zero-baseline failures, then passed after the parent corrected expiry state. The clock follow-up covers GPS/phone UTC disagreement without loosening future-sample validation. Assertions and targeted boundary mutations reviewed; commands and caveats documented in the test README.
