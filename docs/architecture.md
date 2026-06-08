# Architecture

```text
Telegram Bot / Commerce Server
  - users
  - plans
  - orders
  - payment status
  - provisioning jobs
  - agent API

Iran Agent
  - outbound HTTPS only
  - pulls jobs
  - writes local x-ui.db
  - returns subscription result

x-ui Iran
  - clients are added to selected inbounds
  - subscription URL is returned to customer
```

## Provisioning model

A plan points to an inbound group. An inbound group contains one or more x-ui inbound tags, for example:

```text
Group: GermanyPool
  in-20010-tcp
  in-20011-tcp
  in-20012-tcp
```

After payment approval, the agent creates one x-ui client and assigns it to every inbound in the group.
