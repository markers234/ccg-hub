# CCG Hub / Smudge Society

Single-file site (`index.html`) — black & white "CCG" theme and a purple
"Smudge Society" theme in one page, with a Wheel, Chat, Polls, Dice,
Account, and Privacy Policy.

## Version history (this is your "Git section")
This folder is a git repo. Every future change becomes a commit, so you
always have every past iteration:

```
git log --oneline          # see all past versions
git show <commit>:index.html > old.html   # pull out an old version
git diff <commit1> <commit2>              # compare two versions
```

Push it to GitHub to back it up and unlock free hosting:
```
git remote add origin https://github.com/<you>/ccg-hub.git
git push -u origin main
```

## Getting a domain + hosting it
1. Buy a domain (Namecheap, Google Domains successor Squarespace Domains,
   Cloudflare Registrar — any registrar works).
2. Pick a static host and connect your GitHub repo to it:
   - **Netlify** or **Vercel** — drag-and-drop `index.html`, or connect
     the GitHub repo for auto-deploy on every push. Both let you attach
     your custom domain for free.
   - **GitHub Pages** — free, built into the repo you just pushed.
3. Point your domain's DNS at the host (each host gives you the exact
   records to add — usually a CNAME or a couple of A records).

## Important: this site currently relies on Claude's built-in storage
The Wheel, Chat, and Polls currently read/write through Claude's `db`
capability, which **only works while the page is opened as a Claude
artifact** — it will not work once hosted on your own domain. To keep
those features working after you move off claude.ai, swap the `db.*`
calls in `index.html` for a real backend, e.g.:
- **Firebase Firestore** (free tier, very close to the current API —
  `doc()`, `collection()`, `onSnapshot()` all carry over conceptually)
- **Supabase** (Postgres + realtime subscriptions, also free tier)

Everything else — the Wheel spin, Dice, theme switch, the account name
field — already works with no backend at all.

## Enabling "Continue with Google"
The button is already wired up in `index.html`, just switched off. To
turn it on once you have a real domain:
1. Go to console.cloud.google.com → APIs & Services → Credentials.
2. Create an **OAuth Client ID**, type **Web application**.
3. Under **Authorized JavaScript origins**, add your live domain
   (e.g. `https://ccghub.com`) — Google only allows this login from
   domains you register here, so it can't be turned on until you have
   the real domain live.
4. Copy the Client ID into `CONFIG.GOOGLE_CLIENT_ID` near the top of
   the `<script>` block in `index.html`.
5. Commit and redeploy.

Note: this gets you the name/email from a person's Google account for
display purposes. It is not a secure server-verified login — for that,
the token Google returns would need to be checked by a backend (the
same backend you'd add for Firebase/Supabase above can do this).

## Twitter/X feed
Not included — X requires paid API access to read another account's
posts, and any key would be exposed in this client-side page. If you
want this later, the cleanest path is a small backend endpoint that
holds the API key and the page calls that endpoint instead of X
directly.
