#!/bin/sh
# One-shot RabbitMQ provisioner: creates the per-service broker users, their
# scoped permissions, and the transcription-failed audit queue.
#
# Run inside the curlimages/curl image by the rabbitmq-provisioner service in
# both docker-compose.yml (dev) and docker-compose.prod.yml (prod) — this is
# the single canonical copy; it used to be duplicated inline in each file.
#
# Requires: RABBITMQ_BOOTSTRAP_USER, RABBITMQ_BOOTSTRAP_PASSWORD,
# RABBITMQ_BACKEND_PASSWORD, RABBITMQ_WORKER_PASSWORD, RABBITMQ_CLINICAL_PASSWORD.
set -e

api="http://rabbitmq:15672/api"
auth="${RABBITMQ_BOOTSTRAP_USER}:${RABBITMQ_BOOTSTRAP_PASSWORD}"
# The broker healthcheck (node ping) can pass before the management HTTP
# listener on 15672 accepts connections. Wait for the API to answer so we
# do not fail with a connection error (curl exit 7).
echo "waiting for rabbitmq management api..."
until curl --fail --silent --show-error -u "$auth" "$api/overview" >/dev/null 2>&1; do
  sleep 2
done
put_user() {
  curl --fail --silent --show-error -u "$auth" \
    -H "content-type: application/json" \
    -X PUT "$api/users/$1" \
    --data-raw "{\"password\":\"$2\",\"tags\":\"\"}"
}
# Body is passed as a single pre-formed JSON string. It must be single-
# quoted at the call site so the POSIX shell keeps the "\\." backslashes
# literal; nested double quotes would collapse them to "\." and RabbitMQ
# rejects the body as not_json.
put_permissions() {
  curl --fail --silent --show-error -u "$auth" \
    -H "content-type: application/json" \
    -X PUT "$api/permissions/%2F/$1" \
    --data-raw "$2"
}
put_user backend-clinical-knowledge "${RABBITMQ_BACKEND_PASSWORD}"
put_user backend-outbox-relay "${RABBITMQ_BACKEND_PASSWORD}"
put_user transcription-worker "${RABBITMQ_WORKER_PASSWORD}"
put_user clinical-knowledge "${RABBITMQ_CLINICAL_PASSWORD}"
# Consumers declare the shared exchange (configure) and bind their queue to
# it (write on queue + read on exchange), so both need the exchange in their
# configure and read scopes in addition to their own queue namespace.
# They also need write on the default exchange (amq.default): the main queue
# is declared with x-dead-letter-exchange="" (RabbitMQ validates write on the
# DLX at declare time), and poison messages are re-published to the .dlq queue
# by name through the default exchange. Automatic retries are disabled, so there
# are no .retry.* queues - each subscriber runs one live queue plus its .dlq.
put_permissions backend-clinical-knowledge '{"configure":"^medicalassistant\\.events$|^medicalassistant\\.backend\\.transcript-ready(\\.dlq)?$","write":"^amq\\.default$|^medicalassistant\\.events$|^medicalassistant\\.backend\\.transcript-ready(\\.dlq)?$","read":"^medicalassistant\\.events$|^medicalassistant\\.backend\\.transcript-ready(\\.dlq)?$"}'
put_permissions backend-outbox-relay '{"configure":"^$","write":"^medicalassistant\\.events$","read":"^$"}'
# Clinical Knowledge publishes only: it declares the shared exchange (configure) and
# publishes to it (write); it never consumes, so read is empty.
put_permissions clinical-knowledge '{"configure":"^medicalassistant\\.events$","write":"^medicalassistant\\.events$","read":"^$"}'
put_permissions transcription-worker '{"configure":"^medicalassistant\\.events$|^medicalassistant\\.transcription-worker(\\.dlq)?$","write":"^amq\\.default$|^medicalassistant\\.events$|^medicalassistant\\.transcription-worker(\\.dlq)?$","read":"^medicalassistant\\.events$|^medicalassistant\\.transcription-worker(\\.dlq)?$"}'
# consultation.transcription-failed.v1 is emitted but has no subscriber, so
# the outbox relay's mandatory publish returns unroutable and retries forever.
# Give it a durable, bounded audit queue (visible in the management UI) so the
# publish routes to a real destination. Idempotent and matches the direct,
# durable exchange the consumers declare.
curl --fail --silent --show-error -u "$auth" -H "content-type: application/json" \
  -X PUT "$api/exchanges/%2F/medicalassistant.events" \
  --data-raw '{"type":"direct","durable":true}'
curl --fail --silent --show-error -u "$auth" -H "content-type: application/json" \
  -X PUT "$api/queues/%2F/medicalassistant.transcription-failed" \
  --data-raw '{"durable":true,"arguments":{"x-max-length":10000,"x-overflow":"drop-head"}}'
curl --fail --silent --show-error -u "$auth" -H "content-type: application/json" \
  -X POST "$api/bindings/%2F/e/medicalassistant.events/q/medicalassistant.transcription-failed" \
  --data-raw '{"routing_key":"consultation.transcription-failed.v1"}'
