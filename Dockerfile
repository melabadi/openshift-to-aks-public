FROM debian:bookworm-slim
RUN apt-get update \
 && apt-get install --no-install-recommends -y mono-runtime libmono-system-web-extensions4.0-cil \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY dist/ /app/
ARG RELEASE=local
ENV CLAIMS_RELEASE=$RELEASE
ENV CLAIMS_PREFIX=http://*:8080/ HOME=/tmp
USER 10001
EXPOSE 8080
ENTRYPOINT ["mono", "/app/Claims.exe"]
