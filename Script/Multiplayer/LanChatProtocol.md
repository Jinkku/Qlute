# LAN chat wire protocol

Implemented in `LanChat.cs`. Every Qlute build that ever shipped LAN chat must be able to talk to every other one,
so **this format only grows. Nothing in it is ever renamed, removed, or given a new meaning.**

## Transport

- UDP, IPv4. One JSON object (UTF-8) per datagram, at most 8 KiB.
- **Presence port `8913`** (frozen). Every game binds it with `SO_REUSEADDR` and broadcasts `hello` to it
  (to 255.255.255.255, each interface's subnet broadcast, and 127.0.0.1).
- **Direct port**: every game also binds its own ephemeral port. All packets are *sent* from it, and every packet other than `hello`
  goes *to* a peer's direct port (the source port of its `hello` / its `port` field).

## Envelope (frozen, present on every packet)

| field | type   | meaning |
|-------|--------|---------|
| `q`   | string | always `"qlute-chat"`; anything else is dropped |
| `v`   | int    | sender's protocol version (currently `1`) |
| `t`   | string | packet type |
| `id`  | string | sender's random instance id (≤ 64 chars, new every launch) |

## Packet types (v1)

| `t`       | fields | reliable | notes |
|-----------|--------|----------|-------|
| `hello`   | `name`, `game` (game version), `port` | no, repeats every 2 s | a peer that's quiet for 7 s is gone |
| `bye`     | (none) | no | sent on exit |
| `msg`     | `mid`, `room`, `text` (≤ 500), `ts` (unix ms), `name` (optional, sender's current name; fall back to the `hello` name) | yes | `room` = `"lan"` for the public room, otherwise a private room id |
| `invite`  | `mid`, `room`, `title`, `members: [{id, name}]`, `name` (optional, inviter's current name) | yes | |
| `join`    | `mid`, `room` | yes | sent by the invitee to every member after accepting |
| `leave`   | `mid`, `room` | yes | |
| `decline` | `room` | no | |
| `ack`     | `mid` | no | the answer to any packet carrying `mid` |

A "reliable" packet has a `mid` (a unique id). The receiver always answers with `ack` (duplicates too) and handles each
`id/mid` only once. The sender resends every 0.6 s, up to 5 times, until it gets the ack.

Packets from an `id` we haven't had a `hello` from are ignored. Private `msg`s from non-members are ignored and
not acked, so the sender retries once its `join` has landed.

## Compatibility rules (read before changing anything)

1. **Ignore what you don't understand.** Receivers must skip unknown `t` values and unknown fields, and must treat a
   field with the wrong JSON type as missing. They must never drop a whole packet because of an extra field.
2. **Missing fields get defaults.** A new field must have a sensible default when older clients leave it out.
3. **Only add.** New features add new packet types or new optional fields. A field, packet type, room id (`"lan"`), the
   port, or the tag is never renamed, removed, or changed in meaning, and its name is never reused.
4. **Bump `v` when you add something**, and record it in the changelog below. If a feature needs the other side to
   understand it, check the peer's `v` (from its `hello`) and fall back to v1 behaviour for older peers.
5. **Never raise a limit you send against older peers.** Text over 500 chars or packets over 8 KiB get cut or dropped
   by v1 clients. If longer data is ever needed, add a new chunked packet type.
6. If a truly breaking change is ever unavoidable, make it a *new* packet type next to the old one and keep sending
   the old one too. Don't change port 8913 or `q`.

## Privacy

The "Show me in the LAN chat" setting (`lanchat`) closes both sockets, so a game with it off sends and receives nothing.
Turning it off sends `bye` and `leave` for every private room first.

## Changelog

- **v1**: presence, public `#lan` room, private rooms with invites, acked delivery.
