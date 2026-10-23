# NAG-040 — homepage and role workspace

| Task | Owner | Acceptance |
|---|---|---|
| T-040.1 | Redwan | Real root/dashboard page, role-filtered local action catalog, public feature links and CSRF-protected browser logout. |
| T-040.2 | Rajiya | Responsive accessible landing/workspace cards, source/freshness guidance and navigation. |
| T-040.3 | Labiba | Role visibility/local URL tests and guest/resident browser sign-in/navigation/sign-out verification. |

The homepage is `/` (alias `/dashboard`), replacing the old role redirect. Guest
users see public features and sign-in/register entry points. Authenticated users
also see actions appropriate to their claims; actual feature endpoints still enforce
authorization. No invented statistics, counts or live feed are shown. Sign out
deletes the browser auth cookie through a CSRF-protected POST; it does not claim
to revoke previously issued bearer tokens held elsewhere.
