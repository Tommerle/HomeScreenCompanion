# 🎬 Home Screen Companion for Emby



https://github.com/user-attachments/assets/74f6d998-6b59-42b6-b335-261ca6cc6645




**Home Screen Companion** is the ultimate plugin for your Emby server. It automates the management of tags, collections, and playlists for your movies and TV shows, while keeping everyone's home screens perfectly synchronized and looking great.

Whether you want to pull trending lists from Trakt, build smart dynamic playlists from your own library, get AI-powered recommendations, or effortlessly copy your perfect home screen layout to all your users — Home Screen Companion does the heavy lifting for you. Sit back and let your server manage itself!

> [!IMPORTANT]
> Check each release for required server version.

---

## ✨ Key Features

- **5 Powerful Source Types:** Import from external list services, create rules-based "Smart Playlists", leverage AI recommendations, or use your existing local collections and playlists.
- **Automatic Tagging & Collections:** Items are automatically tagged, untagged, and managed in Emby Collections as your sources update.
- **Advanced Scheduling:** Control exactly when a list is active. Perfect for seasonal content like "Christmas Movies" or weekly "Friday Night Action".
- **Dynamic Home Screen Sections:** Automatically inject dedicated rows onto the home screen based on tags or collections. The plugin creates, updates, and cleans them up automatically.
- **Home Screen Sync:** Created the perfect home screen layout? Sync it to all your other users with a single click—or keep it synced automatically!
- **User Playlists:** Create and sync dynamic Emby Playlists directly to specific users based on any of your sources.
- **Continue watching bump:** Never miss a new season premiere! Enables new seasons and/or episode releases to bump up the show to first position in the continue watching

---

## 🚀 Source Types

### 1. External Lists (Trakt & MDBList)
Connect your Emby server to popular and curated lists on **Trakt** or **MDBList**. The plugin fetches the lists regularly, matches the movies/shows to your library, and updates your tags and collections automatically.
* **Supports:** Trakt (Trending, Popular, Watched, User Lists) and MDBList (Dynamic lists).
* *API key required for the respective service.*

### 2. Smart Playlists
Build dynamic lists right from your own library using our flexible, built-in rule engine. No external services required!
* **Filter by:** Resolution (4K, 1080p), Audio Format (Atmos, TrueHD), Genre, Rating, Watch Status (Played/Unplayed), Date Added, and much more.
* **Examples:** *"All Unplayed 4K HDR movies with Atmos"* or *"Action movies from 2020+ rated above 7"*.

### 3. AI-Generated Lists
Let Artificial Intelligence curate content for you! Just write a simple prompt (e.g., *"Best psychological thrillers from the 90s"*) and the AI will generate a list matched perfectly against your library.
* **Supports:** OpenAI (GPT-4o-mini), Google Gemini, and local Ollama models (e.g., Llama 3).
* **Personalized:** You can optionally include a user's watch history so the AI tailors the recommendations just for them!

### 4. Local Collections
Tag items based on an existing Emby Collection. Combine this with scheduling to create time-limited promotions of curated content.

### 5. Local Playlists
Works just like Local Collections, but uses an existing Emby Playlist as the source.

---

## ⏰ Smart Scheduling

Only want to show certain collections at specific times? No problem!
* **Annually:** Make your "Holiday Movies" collection active only from December 1st to 31st every year.
* **Weekly:** Activate a "Weekend Binge" list only on Fridays and Saturdays.
* **Specific Dates:** Perfect for one-time events or themed weeks.

When the schedule ends, the plugin automatically cleans up the tags and collections from your server.

---

## 📺 Home Screen & Syncing

Take total control over how Emby looks for you and your users.

### Per-Entry Home Screen Sections
Add a custom row to the home screen for any specific tag or collection. The plugin handles everything—no manual Emby configuration required. Just select which users should see the section, customize the sorting and display style, and the plugin rolls it out.

### Home Screen Sync
Ever get frustrated trying to give all your users the same great home screen experience?
With **Home Screen Sync**, you choose one user as the "Master" and copy their entire home screen layout to any (or all) other users. You can even set it to sync automatically in the background!

### Continue watching bump
This is a workaround to highlight for the user that a new episode/season has been added. For example, if the user have watched all episode of a show up until s03e10 (season finale). It might then take a year or two before s04e01 is released, and the show will get buried way down in the continue watched, This function manipulates the "LastPlayedDate" for s03e10 to look like the user just played it.
In this way s04e01 will be placed as the first item in the continue watching row.
This bump will only happen once, and if the user doesn't watch the show, it will gradually move down the continue watching list.

When enabled the function auto-detects new seasons and apply the setting. There is also a new scheduled task to catch anything that has been missed (runs default every 6 hour).

You as an admin can select if this should apply to only new seasons, or between all episodes.

---

## 🛡️ Safety & Reliability

* **Fail-Safe Cleanup:** If an external list fails to download, the plugin skips the cleanup process for that tag, ensuring your library never accidentally loses data.
* **Dry Run Mode:** Want to test your rules without changing anything? Turn on Dry Run to see exactly what *would* happen in the logs.
* **Live Logging:** Follow the execution in real-time directly from the plugin's settings page. Every group gets a clear ✔ / ⚠ / ✖ result and the run ends with a summary of what changed.
* **Extended Log:** Turn it on in Settings to get source details, per-item changes, timings and error details — perfect for troubleshooting or bug reports.
* **Log Missing Movies and Shows:** Enable it in Settings and the log lists, per group, which titles from your External and AI lists are not in your library yet.
* **Backup & Restore:** Download your configuration as a JSON file and pick what to include — general settings, API keys, tag & collection groups, saved filters, top lists (including the movie lists of manual top-lists) and home screen sync. Restoring lets you choose the same sections again; each one replaces the current configuration on the server. Generated data (tags, collections, playlists, top-list files, images) is not part of the backup — the plugin rebuilds it on the next sync run.

---

## ⚙️ Installation & Setup
The plugin can be found in the plugin catalogue. 

Manual install:
1. Download the latest `.dll` file from the [Releases](../../releases) page.
2. Shut down your Emby Server.
3. Place the `.dll` file in your Emby `plugins` folder.
4. Start your Emby Server.
5. Go to your Emby Dashboard—you will find **Home Screen Companion** in the sidebar menu!

*(Don't forget to enter your API keys in the **Settings** tab if you plan to use External Lists or AI features).*

---

### Screenshots

<details>
<summary>Expand to view screenshots</summary>

Group overview
<img width="1440" height="900" alt="01" src="https://github.com/user-attachments/assets/44592513-e200-4234-85c2-e010825f3175" />

External source example setup
<img width="902" height="633" alt="02" src="https://github.com/user-attachments/assets/4dcd1345-b116-4435-b82a-acc056a41bb6" />

External source test
<img width="1440" height="900" alt="03" src="https://github.com/user-attachments/assets/2b74e94e-5562-43ef-b60d-7b77faae592a" />

AI list example setup
<img width="902" height="1003" alt="04" src="https://github.com/user-attachments/assets/7dc3cf0c-61c8-4afd-8bf1-b9e2a23890a8" />

Smart Playlist example setup
<img width="902" height="875" alt="05" src="https://github.com/user-attachments/assets/a0de75d6-0403-42d4-ab9a-4c99c33ad92b" />

Collection creation example
<img width="902" height="2168" alt="06" src="https://github.com/user-attachments/assets/378e107c-daea-4d31-b960-2a303dec777b" />

Schedule example
<img width="902" height="578" alt="07" src="https://github.com/user-attachments/assets/b91c2dd7-5616-452b-8042-3561f03c68c8" />

Home screen section creation example
<img width="902" height="1284" alt="08" src="https://github.com/user-attachments/assets/b7f2e3a3-639a-461c-879d-05b976e698c1" />

Top-list settings page
<img width="1440" height="900" alt="09" src="https://github.com/user-attachments/assets/fb7a374c-5840-405b-a5ed-5f87763d4174" />

Top-list creation example
<img width="902" height="804" alt="10" src="https://github.com/user-attachments/assets/df55b486-1fb0-4484-b193-3c41315e8712" />

Top 10 poster customize
<img width="1440" height="900" alt="11" src="https://github.com/user-attachments/assets/42ab2b6b-6602-4879-8924-284894bd47a3" />

Home Screen Section sorting
<img width="1440" height="900" alt="12" src="https://github.com/user-attachments/assets/73ffd033-6e91-4bc6-acf8-568918723863" />

Cleanup function
<img width="1440" height="900" alt="13" src="https://github.com/user-attachments/assets/6c84970e-1eb1-496a-8eee-1bee67b4761f" />














</details>

---

*Disclaimer: This plugin is not affiliated with Emby, Trakt, MDBList, OpenAI, Google, Anthropic or Ollama. This plugin is heavily vibe-coded, tested on my own server — use at your own risk.*
