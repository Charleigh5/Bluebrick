# BB20 Frontend Deployment Contract

The Lab runtime package is ready only when the Lab DLL, Lab config, and exact frontend triplet exist:

- `index.html`
- `assistant-index.css`
- `assistant-web.js`

`scripts/bluebrick.ps1` now verifies source-to-build parity before deployment and build-to-deployed parity before SOLIDWORKS starts. Missing, extra, or mismatched frontend files stop launch with `LAB_PACKAGE_INCOMPLETE` or `LAB_FRONTEND_HASH_MISMATCH`.

Before replacement, the controller backs up the existing Lab DLL, config, frontend directory, runtime manifest, and Lab registration. Rollback restores or removes only those Lab targets. Production paths are comparison evidence only.

Successful staging writes `C:\BlueBrickLab\runtime-manifest.json` with the source commit, run/build ID, Lab bridge port, DLL hash, config hash, and deployed frontend hashes. No credentials or secret values are recorded.
