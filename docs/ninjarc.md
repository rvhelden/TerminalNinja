# ~/.ninjarc — shell aliases & keybindings

The Ninja shell reads `~/.ninjarc` (on Windows: `%USERPROFILE%\.ninjarc`) at startup and evaluates it against the same environment the REPL hands to interactive lines. Anything you can call from the prompt — `alias.set`, `key.bind`, `let`, even `fs.cd` — works here.

A missing rc file is silent. A parse or runtime error is reported to stderr but never blocks startup.

## Shell-mode aliases

Aliases let you type `cd foo` instead of `fs.cd("foo")`. The first token of a line is matched against the alias table; if found and the line is in "shell shape" (no `(`, `=`, `|`, etc.), the remaining whitespace-separated tokens become string arguments to the bound callable. Quoted tokens (`"a b"`) stay as a single argument.

Defaults ship out of the box: `cd`, `ls`, `pwd`, `cat`, `mkdir`, `rm`, `cp`, `mv`, `echo`.

```ninja
# Replace the default cd with one that prints the new directory after changing.
alias.set("cd", path => {
  fs.cd(path)
  println(fs.pwd())
})

# Add a hidden-files-on listing.
alias.set("la", path => fs.ls(path, { hidden: true }))

# Bind a name directly to an existing module function.
alias.set("touch", fs.write)
```

Lines that look like expressions (`cd("foo")`, `cd | print`, `let cd = 1 in cd`) bypass the alias layer and parse as expression syntax — typing `cd foo` and `cd("foo")` are both valid and produce the same effect.

`alias.set("bad", "not a function")` is rejected — the second argument must be a callable.

## REPL keybindings

`key.bind(chord, action)` installs a line-editor shortcut. Chord syntax: `[Ctrl+][Alt+][Shift+]Key`. Modifier order doesn't matter on input; it's normalised on the way into the table.

Supported actions:

- `submit` — same as Enter (return the line).
- `abort` — same as Ctrl+C (abandon the line).
- `clear` — emit the ANSI clear-screen escape and redraw the prompt.
- `complete` — same as Tab.
- `edit-config` — open `~/.ninjarc` in VS Code via the `code` CLI. Bound to **`Ctrl+E`** by default; rebind or unbind to taste.
- `history-prev` / `history-next` — recognised, but the minimal line editor has no history yet; binding these is a no-op for now.

```ninja
key.bind("Ctrl+L", "clear")
key.bind("Ctrl+S", "submit")
key.bind("Ctrl+Q", "abort")
key.unbind("Ctrl+E")           # drop the default edit-config binding
key.bind("Alt+,",  "edit-config")  # …or move it somewhere else
```

Pure Shift chords (e.g. `Shift+A`) are deliberately not eligible for binding — they would block normal capital-letter typing. Chords must include `Ctrl` or `Alt` to be intercepted.

## Inspecting the current state

```ninja
alias.list()  # record of all aliases as { name: callable, ... }
alias.get("cd")  # the callable bound to cd, or unit if unbound
key.list()  # record of all keybindings as { chord: action, ... }
```

## Worked example

```ninja
# ~/.ninjarc — drop this in your home directory.

# Aliases.
alias.set("ll", path => fs.ls(path, { hidden: true }))
alias.set("cdh", () => fs.cd(proc.home()))

# Keybindings.
key.bind("Ctrl+L", "clear")
key.bind("Ctrl+D", "abort")   # override the default EOF-on-empty-buffer

# A bare let extends the persistent env, so this is visible from the prompt.
let project = fs.pwd()
```

After saving this file, `ninja` starts with all of the above in place. Type `ll .` to list the current directory including hidden files, `cdh` to jump home, `Ctrl+L` to clear the screen, and reference `project` to recall the working directory you launched from.
