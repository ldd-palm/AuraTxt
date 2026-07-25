====================================================================
 AuraTxt v1.5
 A portable, highly customizable AI text assistant for Windows
====================================================================

Highlight any text, anywhere on screen, and a small action bar pops
up near your cursor. One click translates it, proofreads it, sums
it up, drafts a reply, or sends it to any AI model you configure.

--------------------------------------------------------------------
WHAT YOU GET
--------------------------------------------------------------------
- Instant trigger: drag-select or double-click text in any app
- Built-in free Google Translate + Youdao Dictionary, no API key needed
- Connect any OpenAI-compatible or Gemini API for custom AI actions
- Interactive window: type an instruction, get AI output (email
  replies, grading, drafting, etc.)
- Global hotkeys for any action, no mouse needed
- Text-to-speech, Google search, 6 built-in color themes
- Fully portable: everything lives next to AuraTxt.exe
- Auto-update check on startup (toggle it off from the tray's
  About window)

--------------------------------------------------------------------
QUICK START
--------------------------------------------------------------------
1. Extract this zip to any folder, e.g. C:\Tools\ -- it unpacks
   into an "AuraTXT" subfolder
2. Run AuraTXT\AuraTxt.exe -- a small icon appears in the system tray
3. Highlight some text in any application and try the action bar
4. To configure providers, models, and actions: right-click the
   tray icon -> Settings (opens auracfg.exe)

No API key is required to start -- Google Translate and Youdao
Dictionary work out of the box.

--------------------------------------------------------------------
UPGRADING -- PLEASE READ
--------------------------------------------------------------------
This package includes a ready-to-use config.json, prompts/,
profiles/, and themes/ so it works immediately after unzipping.

If you already have AuraTxt installed and have customized your own
actions, prompts, or API keys: do NOT extract this zip on top of
your existing folder. Doing so will overwrite those files with the
defaults included here and your customizations will be lost.

Safe ways to upgrade:
  a) Extract this zip to a NEW folder, then copy your old
     config.json, prompts\, profiles\, and themes\ over from your
     previous install, OR
  b) Back up config.json, prompts\, profiles\, and themes\ from
     your old folder before overwriting it, then restore them
     afterwards.

Tip: auracfg automatically keeps a config.json.bak on every save
from its Settings tool. If something goes wrong there, run
"auracfg restore" to recover the last config.json.bak. This does
NOT protect against a zip overwrite -- always back up manually
before upgrading.

--------------------------------------------------------------------
CREATING A CUSTOM ACTION
--------------------------------------------------------------------
Actions are what show up in the action bar. To add your own:

1. Right-click the tray icon -> Settings (or run auracfg.exe
   directly)
2. From the main menu, go to Action Features -> Add Action
3. Follow the prompts:
   - Action ID (no spaces, e.g. "summarize")
   - Icon name from lucide.dev (e.g. "list")
   - Model to use (pick from your configured providers, or a
     built-in one)
   - Whether it's interactive (adds a second pane for typing your
     own instruction alongside the AI output)
   - Prompt file (use {SelectedText} for the highlighted text and,
     for interactive actions, {UserInput} for what you type)
   - Hotkey (optional)
   - Enabled and display order
4. Save from the main menu -- the new action appears in the action
   bar immediately, no restart needed

--------------------------------------------------------------------
MORE HELP
--------------------------------------------------------------------
Project home:  https://github.com/ldd-palm/AuraTxt
Releases:      https://github.com/ldd-palm/AuraTxt/releases

Full documentation (README.md) is available on the project home
page above.
