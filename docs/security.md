# Security Notes

- Do not run the Commerce Server on the Iran entry server.
- Do not use SSH from Commerce to Iran for provisioning.
- Keep Agent secret long and random.
- Run only one Telegram bot instance per token.
- Back up `/etc/x-ui/x-ui.db` before production tests.
- Keep `/etc/x-ui/x-ui.db` writable only by root/x-ui context.
- Do not log client UUIDs or subscription URLs in public logs.
- Start with manual payment approval before Telegram Stars automation.
