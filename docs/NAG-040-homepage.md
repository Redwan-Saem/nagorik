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

The brand opens home on every Razor page. At phone widths, Menu reveals the
navigation and Escape closes it; links remain available if JavaScript is disabled.
Sign-in preserves a return URL and the homepage does not invent activity metrics.

Verification: ten role-navigation cases passed. Desktop (1280px) and phone (390px)
Chrome workflows passed for guest links, resident sign-in, authorized workspace
cards, no admin cards, real loadshedding navigation, brand return and sign-out.
Both cases passed again after the mobile menu adjustment. Captured desktop/mobile
screenshots were inspected; no horizontal overflow was found.
