# Basecamperr — Going Live

This is the full path from "files on my computer" to "a real domain, with a
real backend, that all your friends can use." Every tool below has a free
tier big enough for this scale of site — the **only unavoidable cost is
the domain itself** (about $9–10/year).

Stack used (all free at this scale):
- **Firebase Hosting** — serves the site, free SSL, free CDN
- **Firebase Firestore** — the database behind Basecamps/Wheel/Polls
- **Firebase Authentication** — anonymous IDs for Wheel/Polls, "Continue
  with Google" + a claimed username for Basecamps
- **Cloudflare Registrar** — sells domains at cost, no markup (cheapest
  place to buy one)

`index.html` in this folder is already wired up to call Firebase — you're
just filling in the blanks and deploying it.

## How Basecamps work
- Signing in with Google and claiming a username unlocks the
  **Basecamps** tab.
- Creating a basecamp makes you its **admin**. Only the admin can add
  members (by username) or create rooms.
- Each room the admin creates gets a random **3-letter code**; any
  existing member can jump straight into a room by entering that code
  under Basecamps → Join.
- The admin, or whoever created a specific room, can delete that room
  (this also deletes its messages).

---

## Part 0 — Test locally the right way (fixes "it doesn't work on my computer")

If you just double-click `index.html` and open it as a `file://` link,
the browser blocks the module script that loads Firebase, and depending
on your browser that can silently break the *entire* page, not just the
Firebase parts. `index.html` now loads Firebase defensively (wrapped so
a failure there won't take down the rest of the site), but the real fix
is: **always test through a local web server, never by double-clicking
the file.** Since this folder is already a Firebase project:

```bash
firebase serve
```

then open the `http://localhost:5000` link it prints. (No Firebase
project yet? `python3 -m http.server 8000` from this folder and open
`http://localhost:8000` works too, though Basecamps/Chat/Polls need
real Firebase to do anything.)

## Part 1 — Create the Firebase project (free)

1. Go to https://console.firebase.google.com and sign in with any Google
   account.
2. Click **Add project** → name it (e.g. `ccg-hub`) → you can decline
   Google Analytics (not needed) → **Create project**.
3. In the left sidebar, click **Build → Firestore Database** → **Create
   database** → choose a region close to your friend group → start in
   **Production mode**.
4. Still in the left sidebar, click **Build → Authentication** → **Get
   started**. On the **Sign-in method** tab, enable:
   - **Anonymous** — this is what gives every visitor a stable ID for
     polls/chat without forcing them to log in.
   - **Google** — pick a support email when prompted, then **Save**.

## Part 2 — Set Firestore's security rules

Basecamps are admin-gated now, so the rules are more specific than a
single blanket rule. In Firestore, click the **Rules** tab and replace
the contents with:
```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Wheel + Polls: open to any signed-in visitor (anonymous counts)
    match /wheel/{doc} { allow read, write: if request.auth != null; }
    match /polls/{doc} { allow read, write: if request.auth != null; }

    // Username directory: anyone can check if a name is taken,
    // only the owning account can claim/release it
    match /usernames/{name} {
      allow read: if request.auth != null;
      allow create: if request.auth != null && request.resource.data.uid == request.auth.uid;
      allow delete: if request.auth != null && resource.data.uid == request.auth.uid;
    }

    // A user's own profile + their own membership list
    match /users/{uid} {
      allow read, write: if request.auth != null && request.auth.uid == uid;
      match /memberships/{cid} {
        allow read, write: if request.auth != null && request.auth.uid == uid;
      }
    }

    // A room code points to a community+room; needed to "join by code"
    match /roomCodes/{code} {
      allow read: if request.auth != null;
      allow create, delete: if request.auth != null;
    }

    match /communities/{cid} {
      allow read: if request.auth != null;
      allow create: if request.auth != null && request.resource.data.ownerId == request.auth.uid;
      allow update, delete: if request.auth != null && resource.data.ownerId == request.auth.uid;

      match /members/{uid} {
        allow read: if request.auth != null;
        allow create, delete: if request.auth != null &&
          get(/databases/$(database)/documents/communities/$(cid)).data.ownerId == request.auth.uid;
      }

      match /rooms/{rid} {
        allow read: if request.auth != null &&
          exists(/databases/$(database)/documents/communities/$(cid)/members/$(request.auth.uid));
        allow create: if request.auth != null &&
          get(/databases/$(database)/documents/communities/$(cid)).data.ownerId == request.auth.uid;
        allow delete: if request.auth != null && (
          resource.data.createdBy == request.auth.uid ||
          get(/databases/$(database)/documents/communities/$(cid)).data.ownerId == request.auth.uid
        );

        match /messages/{mid} {
          allow read, create: if request.auth != null &&
            exists(/databases/$(database)/documents/communities/$(cid)/members/$(request.auth.uid));
          allow delete: if request.auth != null &&
            get(/databases/$(database)/documents/communities/$(cid)).data.ownerId == request.auth.uid;
        }
      }
    }
  }
}
```
Click **Publish**. In plain terms: only a basecamp's owner can add
members, create rooms, or delete anyone's messages; only members can
read/post in a room; a room's creator (or the basecamp owner) can
delete that room.

## Part 3 — Get your config and paste it in

1. In Firebase, click the **gear icon → Project settings**.
2. Scroll to **Your apps** → click the **</>** (web) icon → nickname it
   (e.g. `web`) → **Register app**. Don't add the hosting SDK yet.
3. It shows a `firebaseConfig` object. Copy it.
4. Open `index.html` in this folder, find this block near the top of the
   `<script type="module">` tag, and paste your values in:
   ```js
   const firebaseConfig = {
     apiKey: "...",
     authDomain: "...",
     projectId: "...",
     storageBucket: "...",
     messagingSenderId: "...",
     appId: "..."
   };
   ```
5. Save the file.

## Part 4 — Install the Firebase CLI and deploy

This project's `firebase.json` / `.firebaserc` are already set up (you've
already run `firebase init hosting`), so you can likely skip straight to
the deploy command below. If you ever need to redo it from scratch:

You need Node.js installed first (https://nodejs.org — the LTS version).
Then, in a terminal, inside this folder:

```bash
npm install -g firebase-tools
firebase login                 # opens a browser to sign in with Google
firebase init hosting
```

When `firebase init hosting` asks questions, answer:
- **Use an existing project** → pick the one you made in Part 1.
- **What do you want to use as your public directory?** → `.` (a single
  dot — this folder already contains `index.html`).
- **Configure as a single-page app?** → `No`.
- **Set up automatic builds with GitHub?** → `No` (you can add this
  later — see Part 7).
- If it asks to overwrite `index.html` → **No**.

Then deploy:
```bash
firebase deploy --only hosting
```

It prints a URL like `https://ccg-hub.web.app` — open it. The site is
now live on the internet, with a real database, for free. This is a
good moment to test Basecamps/Wheel/Polls with a friend before buying
a domain.

## Part 5 — Buy the cheapest domain

1. Go to https://domains.cloudflare.com and search your desired name.
   Cloudflare charges its wholesale cost with no markup — usually the
   cheapest legitimate registrar for a `.com` (~$9–10/year; some
   endings like `.org` are similar, some like `.io` cost more).
2. Sign up for a free Cloudflare account if you don't have one, and
   complete the purchase. Cloudflare requires 2-factor auth on the
   account — use an authenticator app or security key.

## Part 6 — Connect the domain to Firebase Hosting

(You've already started this — DNS verification is in progress. The
steps below are here for reference / if you need to redo any of it.)

1. Back in Firebase Console → **Hosting** → **Add custom domain**.
2. Type your domain (e.g. `ccghub.com`) → **Continue**.
3. Firebase shows you a **TXT record** to prove ownership, and then
   **A records** to point the domain at Firebase's servers.
4. In Cloudflare, go to your domain → **DNS** → **Records** → add each
   record Firebase gave you exactly as shown (same type, name, value).
   For the A records, set the Cloudflare "Proxy status" to **DNS only**
   (grey cloud, not orange) so Firebase can issue its SSL certificate.
5. Back in Firebase, click **Verify**. DNS usually propagates within
   minutes to a few hours (rarely up to 24h); Firebase automatically
   issues a free SSL certificate once it sees the records.
6. Once it shows **Connected**, `https://ccghub.com` (your real domain)
   serves the live site.

## Part 7 (optional) — Auto-deploy on every git push

This folder is already a git repo (`git log` shows your version
history). To make every `git push` automatically redeploy:
```bash
firebase init hosting:github
```
Follow its prompts to connect your GitHub repo — after that, pushing to
`main` deploys automatically via GitHub Actions, no manual
`firebase deploy` needed. (Requires the repo to actually be on GitHub —
`git remote add origin https://github.com/<you>/ccg-hub.git` and
`git push -u origin main` first if you haven't.)

## Costs, summed up
| Item | Cost |
|---|---|
| Domain (Cloudflare Registrar) | ~$9–10/year |
| Firebase Hosting | $0 (free tier) |
| Firestore | $0 (free tier — plenty for a friend group) |
| Firebase Authentication | $0 (free tier) |
| **Total** | **~$9–10/year** |

If you'd rather pay nothing at all, skip Part 5 and just use the free
`https://ccg-hub.web.app` address Firebase gives you in Part 4.

## What's still not included
- **Twitter/X feed** — still not possible without a paid X API key, and
  a backend to hold that key safely (a Cloud Function is the natural
  place if you want this later).
- **Username collisions** — usernames are claimed on a first-come basis
  with no recovery flow if someone forgets theirs; if you want
  password-reset-style account recovery later, that's a bigger Firebase
  Auth feature I can add on request.
- **Removing a member / renaming a room** — the current admin tools are
  add member, create room, delete room. Removing a member or renaming a
  room isn't wired up yet — ask if you want either added.
