# Verification checklist

Use this after installing the plugin and configuring the API.

## 1. Plugin loads

```bash
docker logs valheim 2>&1 | grep -i "Odin Chat"
```

Expected:

```
[Info   : Odin Chat] Harmony patches applied.
[Info   : Odin Chat] Odin Chat 0.1.0 loaded.
[Info   : Odin Chat] API endpoint: ...
```

If the API key is still `sk-REPLACE_ME`, the log also shows a warning. Edit
`config/bepinex/com.odin.valheim.chat.cfg` and restart.

## 2. Shout test

Join the server, wait about two seconds for the player-list sync, then type:

```
/s Odin, reply with exactly: pong
```

Expected:

- `[Odin]` appears in the player list.
- `[Odin]: ...` appears in chat within a few seconds.
- The server log shows `Odin request from <player> (Shout): ...`.

## 3. Say test

Type in normal chat:

```
Odin, reply with exactly: pong
```

Expected: the same kind of reply, with `(Normal)` in the server log.

## 4. Trigger boundaries

These should **not** trigger Odin:

```
odinseye
Odin's beard
hello Odin
```

These should trigger:

```
Odin, ...
odin, ...
odin! ...
odin ...
```

## 5. Two-player test

With both players online:

- Both players' say and shout messages should reach Odin.
- Both should see public replies.
- There should be exactly one `[Odin]` entry, not one per player.
- A question from player A should not be answered twice.

## 6. Fake-player lifecycle and clock

1. Note the in-game day/time or the `World time:` value in the server log.
2. Have the last player disconnect.
3. Confirm `[Odin]` disappears from the player list/social panel after the
   server's next player-list sync.
4. Leave the server empty for a few minutes.
5. Rejoin. The world time should not have advanced while the server was empty.
6. Confirm there is still exactly one `[Odin]` entry.

The `GetNrOfPlayers` postfix excludes `[Odin]`, so the clock, loot scaling, and
capacity check should count only real players.

## 7. Player history

The synthetic `[Odin]` entry must not be written into the world's saved player
history. On world load, the server log prints entries such as:

```
Player history entry with index 0: cloudberry. (Steam_...)
```

You should never see `[Odin]` there.

## 8. Per-player loot

This is easiest to check with a boss progression item such as the Elder's Swamp
Key. With one real player online, a solo kill should give one key, not two. The
synthetic entry is excluded from `GetNrOfPlayers`, which is the value used by
`CharacterDrop.m_onePerPlayer`.

## 9. Error handling

Temporarily set an invalid `BaseUrl` or key and restart. Ask Odin a question.
Expected:

- The server log shows `Odin API request failed: ...`.
- The asker receives a short private error if `ShowErrors = true`.
- The server does not crash or block the game thread.

Restore the correct configuration afterwards.

## 10. Private mode

Set `ReplyVisibility = Private` and restart. Ask a question with two players
online. Only the asker should see the answer. Set it back to `Public` if
desired.

## Common symptoms

| Symptom | Check |
|---|---|
| No `Odin Chat` lines in the log | BepInEx enabled, DLL in `BepInEx/plugins`, correct server image |
| Plug-in loads but no reply | API key/base URL/model, `docker logs` for `Odin API request failed` |
| Question is ignored | Message must start with `odin` and a word boundary; wait 2s after join |
| `[Odin]` missing from player list | Ensure at least one real player is connected; wait for the 2s sync |
| `[Odin]` remains after everyone leaves | Check the server log for `Could not inject` or `GetNrOfPlayers` patch errors |
| Answers appear twice | Check for duplicate plugin DLLs or multiple BepInEx installs |
| API key warning | Edit `config/bepinex/com.odin.valheim.chat.cfg` and restart |
