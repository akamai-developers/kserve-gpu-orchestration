# Add-on: HTTPS with a custom domain (Let's Encrypt)

The default setup serves the .NET app over **plain HTTP on the Gateway's IPv4 address**. This add-on
serves it at `https://api.<your-domain>` with a free Let's Encrypt certificate. It only adds what HTTPS
needs on top of the default setup:

| Already part of the default setup | Added by this add-on |
|---|---|
| Gateway API CRDs, NGINX Gateway Fabric | A DNS `A` record for your hostname |
| cert-manager **with Gateway API support enabled** | Let's Encrypt issuers (`manifests/https/clusterissuers.yaml`) |
| Gateway `public` with an HTTP listener on port 80 | An HTTPS listener on port 443 (`manifests/https/gateway.yaml`) |
| HTTPRoute `api` to the app (attaches to every listener) | An HTTP → HTTPS redirect for your hostname (`manifests/https/httproute-redirect.yaml`) |

No IaC change, no Helm change and no app change is needed. The raw IP keeps working over HTTP; only
requests for your hostname are redirected to HTTPS.

**Out of scope:** registering the domain and delegating it to Linode DNS. This guide assumes the domain
(`example.com` below) is already served by Linode DNS and you want the app at `api.example.com`.

## Prerequisites

- The default setup is running (README steps 1–3), and `KUBECONFIG` points at the cluster.
- For the DNS record: an Akamai Cloud token with **Domains → Read/Write** (in addition to the
  Kubernetes scope from the README), or use Cloud Manager instead.

## 1. Point DNS at the Gateway

```bash
GATEWAY_IP=$(kubectl get gateway public -n gateway -o jsonpath='{.status.addresses[0].value}')
echo $GATEWAY_IP
```

Create an `A` record for `api` in your Linode DNS zone that points to `$GATEWAY_IP`, either in
Cloud Manager (**Domains** → your domain → **Add an A/AAAA Record**) or with `linode-cli`:

```bash
DOMAIN_ID=$(linode-cli domains list --domain example.com --format id --text --no-headers)
linode-cli domains records-create "$DOMAIN_ID" --type A --name api --target "$GATEWAY_IP" --ttl_sec 300

dig +short api.example.com     # must print $GATEWAY_IP before you continue (allow a few minutes)
curl http://api.example.com/healthz   # Healthy: the default HTTP setup already serves the name
```

Let's Encrypt validates by fetching a file from `http://api.example.com/.well-known/acme-challenge/…`,
so the name must resolve to the Gateway first.

## 2. Set your hostname and e-mail address

```bash
HOST=api.example.com          # your hostname
EMAIL=you@example.com         # Let's Encrypt expiry notices go here

sed -i.bak "s/api\.example\.com/$HOST/g; s/you@example\.com/$EMAIL/g" manifests/https/*.yaml
rm manifests/https/*.bak
```

## 3. Apply the add-on

```bash
kubectl apply -f manifests/https/
kubectl get gateway public -n gateway        # now lists the https listener
```

`manifests/https/gateway.yaml` replaces the default Gateway definition with the same HTTP listener plus an
HTTPS listener and the `cert-manager.io/cluster-issuer` annotation. Same name, same NodeBalancer, same IP.

## 4. Get the certificate (staging first)

The Gateway starts with Let's Encrypt's **staging** service: generous rate limits, but browsers don't
trust its certificates. Check that issuance works:

```bash
kubectl get certificate -n gateway -w          # api-tls: READY becomes True (usually under a minute)
kubectl describe certificate api-tls -n gateway
kubectl get challenges -A                     # only while a challenge is pending
```

When `READY` is `True`, switch to **production** and force a new certificate:

```bash
sed -i.bak 's/letsencrypt-staging/letsencrypt-prod/' manifests/https/gateway.yaml && rm manifests/https/gateway.yaml.bak
kubectl apply -f manifests/https/gateway.yaml
kubectl delete secret api-tls -n gateway
kubectl get certificate -n gateway -w
```

cert-manager renews the certificate automatically about 30 days before it expires.

## 5. Test it

```bash
curl -I http://api.example.com/healthz          # 301 → https://api.example.com/healthz
curl https://api.example.com/healthz            # Healthy
curl -N https://api.example.com/ask -H 'content-type: application/json' -d @demo/ask.json
curl http://$GATEWAY_IP/healthz                 # raw IP still works over HTTP
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| No Certificate `api-tls` appears | Gateway is missing the issuer annotation, or cert-manager started before the Gateway API CRDs existed | Check `kubectl get gateway public -n gateway -o yaml`; restart cert-manager: `kubectl rollout restart deploy cert-manager -n cert-manager` |
| Challenge stays `pending` | DNS doesn't point at the Gateway yet | `dig +short api.example.com`, then `kubectl describe challenge -A` |
| HTTPS listener shows `ResolvedRefs: False` | Certificate not issued yet | Wait for step 4; the listener recovers on its own |
| Browser warns about the certificate | Still on the staging issuer | Step 4: switch to `letsencrypt-prod` |
| Rate-limit error from Let's Encrypt | Too many production attempts | Go back to staging until issuance works |

## Removing the add-on

```bash
kubectl delete -f manifests/https/httproute-redirect.yaml -f manifests/https/clusterissuers.yaml
kubectl apply -f manifests/gateway/gateway.yaml     # back to the HTTP-only Gateway
kubectl delete certificate api-tls -n gateway --ignore-not-found
kubectl delete secret api-tls -n gateway --ignore-not-found
```

Then delete the DNS record (Cloud Manager, or `linode-cli domains records-delete <domainId> <recordId>`).

## References

- cert-manager with Gateway API: <https://cert-manager.io/docs/usage/gateway/>
- cert-manager HTTP-01 through Gateway API: <https://cert-manager.io/docs/configuration/acme/http01/>
- NGINX Gateway Fabric: <https://docs.nginx.com/nginx-gateway-fabric/>
