# Odin Chat

Odin Chat is a **server-side** Valheim chat bot. Players ask a question in
ordinary say or shout chat, and the server answers in chat as `[Odin]` using any
OpenAI-compatible chat completions endpoint.

No client mod is required. Vanilla clients work as-is.

```
Odin, how do I tame a boar?
/s Odin, what should I build first?
```

## Features

- Server-side BepInEx plugin; clients stay vanilla.
- Works with ordinary local say and `/s` shout chat.
- Trigger is the case-insensitive word `odin` at the start of the message:
  `Odin, ...`, `odin, ...`, `odin! ...`, `odin ...`.
- OpenAI-compatible API support: OpenAI, OpenRouter, Ollama, LM Studio, vLLM,
  or any other server that implements `POST <base>/chat/completions`.
- Public or private replies.
- Per-player cooldown, question length limit, concurrency limit, and reply
  chunking.
- Configurable system prompt, model, temperature, max tokens, and timeout.
- No conversation memory: every question is independent.

## How it works

Valheim's dedicated server does not count as a player and is not in
`ZNet.m_players`. A server-originated `ChatMessage` RPC is therefore dropped by
vanilla clients because they cannot resolve the sender's display name.

Odin Chat handles that with a synthetic player entry:

1. While at least one real peer is connected, the plugin adds one
   `[Odin]` `ZNet.PlayerInfo` to the server player list.
2. Vanilla clients receive that entry through the normal player-list sync.
   It has a non-empty character id and a synthetic `PlatformUserID`.
3. Vanilla chat sends say/shout messages to every other entry in the player
   list, including `[Odin]`. The dedicated server routes those packets and the
   plugin reads the copies addressed to `[Odin]`.
4. The plugin calls the configured API asynchronously, then injects the answer
   through the vanilla `ChatMessage` RPC, using `[Odin]` as the sender.
5. `GetNrOfPlayers` is patched to exclude `[Odin]`, so the world clock,
   10-player capacity check, and one-per-player loot drops count only real
   players.
6. When the last real player disconnects, the next player-list rebuild clears
   the synthetic entry. It is not re-added while the server is empty.

The plugin uses Harmony to apply these changes **in memory** through BepInEx.
It does not edit `assembly_valheim.dll`, build a custom Valheim server, or
require anything on the client.

## Requirements

- Valheim dedicated server 1.0.x.
- BepInEx 5.4.x. With the `lloesche/valheim-server` image, set
  `BEPINEX=true`; the image installs the pack for you.
- An OpenAI-compatible endpoint reachable from the server container.
- Outbound HTTPS/HTTP access from the server to that endpoint.

The included build was compiled against the Valheim server assembly in the
`lloesche/valheim-server` image as of **2026-10-25**. Valheim updates can change
the internal methods the plugin targets. Rebuild against the new
`valheim_server_Data/Managed` directory after a major update and check the
BepInEx log for Harmony errors.

## Install: lloesche/valheim-server + Docker Compose

1. Put `OdinChat.dll` in `./config/bepinex/plugins/OdinChat.dll`.
2. Enable BepInEx in `compose.yml`:

   ```yaml
   environment:
     BEPINEX: "true"
     # Do not also enable VALHEIM_PLUS; the image rejects that combination.
   ```

3. Start the server once. BepInEx creates the config file at:

   ```
   ./config/bepinex/com.odin.valheim.chat.cfg
   ```

4. Edit the API section and restart:

   ```ini
   [Api]
   BaseUrl = https://api.openai.com/v1
   ApiKey = sk-...
   Model = gpt-4o-mini
   ```

5. Verify the plugin loaded:

   ```bash
   docker logs valheim 2>&1 | grep -i "Odin Chat"
   ```

   You should see `Odin Chat 0.1.0 loaded.` and the endpoint/model it is using.

The image syncs `/config/bepinex/plugins` into the BepInEx install on startup.
It also uses `/config/bepinex` as the BepInEx config directory, so both the
plugin and its config survive container recreation.

## Install: generic BepInEx server

1. Install BepInEx 5.4.x for the Valheim dedicated server.
2. Copy `OdinChat.dll` to `BepInEx/plugins/`.
3. Start the server once, then edit
   `BepInEx/config/com.odin.valheim.chat.cfg`.
4. Restart the server.

## Configuration

All settings live in `BepInEx/config/com.odin.valheim.chat.cfg`. Restart after
editing.

| Setting | Default | Description |
|---|---|---|
| `Api.BaseUrl` | `https://api.openai.com/v1` | Base URL. The plugin posts to `<BaseUrl>/chat/completions`. |
| `Api.ApiKey` | `sk-REPLACE_ME` | API key. Keep the file private. |
| `Api.Model` | `gpt-4o-mini` | Model name understood by your endpoint. |
| `Api.SystemPrompt` | Odin persona | System prompt sent with every request. |
| `Api.Temperature` | `0.7` | Sampling temperature, 0 to 2. |
| `Api.MaxTokens` | `300` | Reply token limit. Set to `0` to omit `max_tokens`. |
| `Api.TimeoutSeconds` | `60` | HTTP timeout. |
| `Chat.Trigger` | `odin` | Case-insensitive prefix. Requires a word boundary. |
| `Chat.ReplyVisibility` | `Public` | `Public` or `Private`. |
| `Chat.CooldownSeconds` | `10` | Per-player cooldown between requests. |
| `Chat.MaxQuestionLength` | `500` | Maximum question length. |
| `Chat.MaxChatChunkLength` | `400` | Long replies are split into chat lines of this size. |
| `Chat.MaxConcurrentRequests` | `1` | Requests allowed in flight at once. |
| `Chat.ShowErrors` | `true` | Send the asker a short private error on API failure. |
| `Debug.VerboseLogging` | `false` | Log trigger, chunk, and injection details. |

### Endpoint examples

OpenAI:

```ini
BaseUrl = https://api.openai.com/v1
ApiKey = sk-...
Model = gpt-4o-mini
```

OpenRouter:

```ini
BaseUrl = https://openrouter.ai/api/v1
ApiKey = sk-or-...
Model = openai/gpt-4o-mini
```

Ollama on the Docker host:

```ini
BaseUrl = http://host.docker.internal:11434/v1
ApiKey = ollama
Model = llama3.1
```

On Linux, add this to the Valheim service so the container can reach the host:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

Any other OpenAI-compatible server works if it accepts the standard
`/chat/completions` request shape. Use `MaxTokens = 0` if your provider rejects
`max_tokens`.

## Usage

Say chat:

```
Odin, how do I tame a boar?
```

Shout chat:

```
/s Odin, where should I put my first portal?
```

The plugin strips the trigger, optional punctuation, and leading whitespace, so
all of these reach the model as `how do I tame a boar?`:

```
Odin, how do I tame a boar?
odin, how do I tame a boar?
odin! how do I tame a boar?
odin how do I tame a boar?
```

`odinseye` and `Odin's beard` do not trigger because the next character is not a
word boundary.

## Security and privacy

- The API key is stored in plain text in the BepInEx config file. Restrict
  permissions and never commit the file.
- The plugin sends the player's name and question to the configured API.
- Replies are public by default. Set `ReplyVisibility = Private` if answers
  should go only to the asker.
- Per-player cooldowns, question length limits, and a concurrency limit reduce
  accidental cost and spam. They are not a substitute for provider-side
  budgets and rate limits.
- There is no conversation history, tool use, or memory. Each request contains
  only the configured system prompt and the current question.

## Limitations

- `[Odin]` is visible in the player list and social panel while at least one
  real player is online. This is what makes the vanilla chat sink work. A
  client-side UI filter could hide it, but that would reintroduce a client mod.
- After joining, wait about two seconds for the player-list sync before asking;
  otherwise the client may not know about `[Odin]` yet and the message will not
  be sent to the server's sink.
- Vanilla clients may show `[Odin]` as an extra active peer in the F2 panel
  because the `GetNrOfPlayers` patch is server-side only. Server-side logic
  (clock, loot, capacity) uses the corrected count.
- No streaming, markdown rendering, conversation memory, or private per-player
  context.
- Built against a specific Valheim assembly. After a Valheim update, rebuild
  and verify the Harmony patches still apply.
- The synthetic entry is a server-side trick, not an official Valheim API. It
  is deliberately simple and contained, but it is still dependent on Valheim
  internals.

## Troubleshooting

**Plugin not loading**

- Check `BepInEx/LogOutput.log` or `docker logs valheim` for `Odin Chat`.
- Confirm `BEPINEX=true`; do not enable `VALHEIM_PLUS` at the same time.
- Confirm the DLL is in the BepInEx `plugins` directory and has no `Zone.Identifier`
  or other wrapper.
- If BepInEx reports a missing assembly, rebuild against the server's current
  `valheim_server_Data/Managed` DLLs.

**No replies**

- Check the log for `Odin request from ...` and `Odin API request failed`.
- Verify `BaseUrl`, `ApiKey`, and `Model`; test the endpoint with `curl` from
  inside the Valheim container.
- Wait a couple of seconds after joining so the `[Odin]` player entry has
  synced.
- Check that the message starts with `odin` followed by whitespace or
  punctuation.
- If `ReplyVisibility = Private`, only the asker sees the answer.

**API key warning**

`ApiKey is still the placeholder` means the config still contains
`sk-REPLACE_ME`. Edit it and restart.

## Building from source

Requirements: .NET SDK 8+ and the Valheim server's Managed DLLs plus the
BepInEx core DLLs.

```bash
# Example: copy references out of a running lloesche/valheim-server container
mkdir -p refs/valheim
docker cp valheim:/opt/valheim/server/valheim_server_Data/Managed/. refs/valheim/

# Extract BepInExPack_Valheim (version should match the server)
mkdir -p refs/bepinex
unzip BepInExPack_Valheim-5.4.2351.zip -d refs/bepinex

export VALHEIM_MANAGED="$PWD/refs/valheim"
export BEPINEX_CORE="$PWD/refs/bepinex/BepInExPack_Valheim/BepInEx/core"

./scripts/build.sh
```

The output is `dist/OdinChat.dll`.

The project targets .NET Framework 4.7.2 and references:

- `BepInEx.dll`, `0Harmony.dll`
- `assembly_valheim.dll`, `assembly_utils.dll`, `Splatform.dll`
- `UnityEngine.dll`, `UnityEngine.CoreModule.dll`
- `Newtonsoft.Json.dll`

These references are not copied into the output; the server provides them.

To build a release zip for GitHub Releases:

```bash
./scripts/package.sh
```

The archive is written to `release/OdinChat-<version>.zip` and contains the
DLL, the example config, and the documentation.

## Verification

See [TESTING.md](TESTING.md) for an in-game checklist covering the fake-player
lifecycle, clock, player history, loot scaling, private replies, and API
failures.

## Credits

The fake-player/chat-relay approach was informed by
[FiresDiscordIntegration](https://github.com/fire-VA/FiresDiscordIntegration)
(MIT), which demonstrates server-side chat injection to vanilla Valheim
clients. Odin Chat is a separate, smaller plugin focused on AI chat.

## License

MIT. See [LICENSE](LICENSE).
