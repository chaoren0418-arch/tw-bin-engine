# Reproduction and evidence

All executed argv/cwd/private environment/exit codes and helper source snapshots are in `evidence/<tag>/*.command.json` and `Program.cs`. Product is built from the full 180-file candidate, with offline vendor, private caches/TEMP, Release deterministic path mapping and `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`.

Executed commands (before sealing, cwd is this work root):

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\run.py candidate candidate-01
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\run.py baseline baseline-01
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\run.py extra extra-01
```

candidate-01 built the product and ran339 checks; baseline-01 used the copied exact accepted r33 DLL and ran18 baseline checks; extra-01 reused the same candidate DLL and ran63 additional checks. Helpers use reflection for internal seams and fixture setup, not reimplemented product methods. `evidence/build-01` is the first compile only and contributes no check count. Current `checks/Program.cs` includes all three modes; candidate/baseline snapshots predate addition of the separate Extra method. No product source changed after candidate-01.

Do not rerun into this sealed root. In a separately authorized fresh work root under the project work directory, independently copy `candidate/source`, `candidate/audio`, `candidate/web`, `baseline-runtime`, `vendor`, `assets`, `checks`, `NuGet.Config`, `test-assets-root.txt`, and `run.py`; create empty `evidence`, `tmp`, `cli-home`, `nuget` directories. Then run the commands above with fresh unique tags. Candidate mode builds the real product; extra mode requires its candidate/build output. No junction is required. Preserve the exact readonly asset root in test-assets-root.txt unless a separately verified equivalent root is provided. The helper calls `new Assets(root)` and also loads PlayerProfiles/ActionCadence from its AppContext ../../assets copies; neither dependency can be omitted.

Run read-only checks in the original sealed root:

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\verify_delivery.py --seal '<published delivery-manifest SHA256>'
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\project_resource.py
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\verify_binding.py
```

Resource generation in a fresh authorized root uses `project_resource.py --write`. It reads only ten accepted decoded JSON files, validates them against the pinned source seal, canonicalizes UTF-8 sorted compact JSON, and requires the239766-byte exact resource hash. It does not decode SPR or scan PAK archives.

Pixel checks independently compare exact expected body/shadow commands, sort depth keys, calculate normal/half RGB555 compositing and coverage/owners, then inspect every pixel of actual BitmapOf output. The selected real scene contains no screen/multiply blends; unexpected such blends fail the oracle rather than silently borrowing product Blit. The candidate test checks model/packet ownership and state transitions; native timing, input latency, device audio and live GUI are separate pending acceptance.

Delivery manifest excludes only itself and transient obj/bin/__pycache__/tmp/nuget/cli-home directories. Those directories are not evidence or deliverable source. No cleanup deletes or attribute changes were performed on shared sources.
