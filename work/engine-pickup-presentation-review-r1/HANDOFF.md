# ENGINE-PICKUP-PRESENTATION-REVIEW-PREP-R1

**Preparation complete; no candidate acceptance.** Baseline is the supervisor-accepted immutable r33 stage, seal6682cd2eedb7b1a7a8e6b5824d3f92e0408b57949a9e78dbeae471a056acd9e7. Product DLL remains8ed92b8be9e532198e0f5bc88e404b47a3601f0e98e0962a0e1820571090d0c2. Formal supervisor contract is pinned at8c60b1061c637bcb7f992d7f92c74fc80c0ad945c7f05d1c3a2d74507cffc6f2.

Read STATIC-REVIEW.md, policy-review.json, coverage.json and test-matrix.json. The exact minimal resource projection and32 frame maps are under expected/. No product resource or product source was changed. All177 baseline source pins, the selected pixels and154 eligibility-source artifact hashes were checked. Final r33 release binding remains a separate pending exact-pin handoff; no canonical source was used.

Accepted baseline run: runs/baseline-05/baselineprobe.json —84 actual occurrence rows:53 assertions and31 observations.32 reference frames match RGBA with zero differing channels.16 renderer active-window assertions include projectile-hit-cache/death fixtures. Five existing-behavior assertions cover focused C112/heading/count/owner invariants. Feature-gap observations show r33 S158 has no pickup animation. Twenty-four candidate risk rows are prepared, all candidate occurrences=0. The full4337 existing suites were not rerun or edited.

Important boundary: damage Duration583.3333333333334 remains visible at age583 and expires at584, although displayed StepMs is583. Preserve existing renderer comparison and history; do not suppress forever merely because OwnCombat is non-null. Hit-cache priority also matters with OwnCombat null. Failed developmental runs remain auditable and are excluded from final totals.

Readonly verification:

`python -B -X utf8 verify.py --seal <published artifact-manifest SHA256>`

Re-run the already built helper into a fresh evidence folder:

`powershell -NoProfile -File run_probe.ps1 -RunId reviewer-01`

This creates only reruns/reviewer-01 under this work root. Reruns are excluded from the original seal and must be evaluated separately. The helper uses the copied exact r33 DLL, not a rebuilt product. Source for rebuilding is harness/Probe.csproj; build only in this assigned work root with local CLI/cache paths, and do not overwrite sealed build outputs.

The scene is synthetic/empty, packets are synthetic, selected asset bodies are accepted originals, and actual Renderer.ComposeObjects/Blit/BitmapOf run offscreen. Reflection seeds Assets and hit-cache fixtures and invokes the transport-independent packet dispatcher. No ClientForm/window, normal engine entrypoint, GUI, native BIN/JAR, DB/services/SQL/socket, candidate execution or shared/original mutation occurred. Native runtimeChecks=0; managed offline rows84 are reported separately. No live timing/input/native-neutral/Slow qualification is granted. Candidate independent review requires a new explicit dispatch.
