# Agent Protocol

The Iran Agent authenticates each request using HMAC headers:

```text
X-Dariche-Agent-Id
X-Dariche-Timestamp
X-Dariche-Nonce
X-Dariche-Signature
```

Signature canonical string:

```text
METHOD
PATH_AND_QUERY
TIMESTAMP
NONCE
BODY
```

Endpoints:

```text
POST /api/agent/heartbeat
GET  /api/agent/jobs/next
POST /api/agent/jobs/{jobId}/result
```

The Commerce Server never SSHs into Iran. The Iran Agent pulls work.
