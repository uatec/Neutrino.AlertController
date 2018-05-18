# Alert Controller

```json
kind: alert/v1
spec:
  metadata:
    team: web-team
    service: somewebsite
    environment: production
  name: Server CPU
  target: movingMedian(stats.gauges.server_001.cpu.average, '15m')
  warn: 80
  error: 90
---
kind: subscription/v1
spec:
  target: production-owners@email.com
  type: email
  selector:
    environment: production
---
kind: subscription/v1
spec:
  target: web-team@email.com
  type: email
  selector:
    team: web-team
```