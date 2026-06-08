# Payment Flow

## MVP manual payment

```text
User: /buy PLAN_CODE
Bot creates PendingPayment order
Bot sends payment instructions
User: /paid ORDER_ID receipt
Order becomes AwaitingAdminApproval
Admin: /approve_order ORDER_ID
Order becomes Provisioning
Provisioning job is created
Iran Agent creates client
Bot sends subscription URL
```

## Later Telegram Stars

Telegram Stars can be added later by mapping a successful payment update to the same order-paid/provisioning flow.
