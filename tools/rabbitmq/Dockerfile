FROM rabbitmq:3.6-management-alpine

RUN apk --no-cache add curl

ADD rabbitmq.config /etc/rabbitmq/
ADD definitions.json /etc/rabbitmq/

CMD ["rabbitmq-server"]