# GDD Backlog

Deferred work — analyzed and intentionally not built (to keep the tool simple), kept
here so it isn't lost.

## Anti-bot fidelity — Tier 3 (full session realism)

The high-leverage pieces are done: opt-in stealth (`AppConfig.Stealth` — disables
AutomationControlled + masks navigator.webdriver/chrome.runtime/permissions/plugins on
the Playwright engines) and a continuous, per-player mouse trajectory across
tap/hover/drag. Tier 3 is the long tail and is **out of scope for a testing tool** —
record only:

- **Scroll with inertia / momentum** instead of fixed-step scrolling.
- **Randomized think-time** between actions (variable pauses, occasional idle
  micro-movements) driven from the session, not per-call.
- **Network/session realism**: residential proxies, IP/ASN reputation, geo consistent
  with the emulated timezone/locale, human-plausible action rates.
- **Challenge solving**: reCAPTCHA v3/Enterprise, DataDome/Kasada/Cloudflare
  proof-of-work — explicitly *not* GDD's job.

Rationale: each adds real complexity for diminishing returns, and pushes GDD from a
testing tool toward an evasion framework. Revisit only against a concrete, named
detection that the current stealth + trajectory don't clear.

## WebView2 / WPF stealth

`Stealth` is wired for the Playwright engines (GDD.Headless, GDD.Desktop) only.
BrowserXn (WPF) uses WebView2 / embedded Edge, which is **not** launched with
`--enable-automation`, so `navigator.webdriver` is already absent and `chrome.runtime`
/ `plugins` are already real — the stealth script is a near-total no-op there and the
launch flag is redundant. Not built to avoid shipping dead, unverifiable code (the
running GDD.exe can't be restarted for a verification pass in-session). Revisit only if
a real WebView2 automation tell turns up.

## Previously analyzed, declined by request

Recorded so the analysis isn't repeated:

- **Native JS dialogs (alert/confirm/prompt) flashing away** — Playwright auto-dismisses
  them with no `page.Dialog` handler. Fix would be a handler that holds the dialog open +
  a beacon telling the agent it's open. (Note: native dialogs can't be captured by
  `page.ScreenshotAsync` regardless — they're browser UI, not page content.)
- **Overlay not captured by gdd_screenshot** — two causes: (1) content outside the
  viewport → use the existing `gdd_scroll` (full-page screenshots would break the
  screenshot↔tap CSS-pixel coordinate invariant, so deliberately not added); (2) content
  in a `window.open` popup → GDD is single-page; a lightweight popup-detection beacon was
  proposed over full multi-window management.
