---
name: OpenAPI Zod compatibility
description: Contract generation relies on Zod 4 integer schemas.
---

Keep the workspace Zod catalog on Zod 4 when using the current Orval output. The generated API validation schemas emit `zod.int()`, which is unavailable in Zod 3.

**Why:** A valid OpenAPI codegen run can still fail the workspace typecheck when the generated schema API and installed Zod major version disagree.

**How to apply:** If codegen reports missing `zod.int`, verify the catalog and installed dependency major version before changing the OpenAPI model.