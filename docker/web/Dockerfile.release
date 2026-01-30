# Define ARG at the top level
ARG NODE_VERSION=22
ARG WEB_BASE_HREF=/
ARG NGINX_RUNTIME_SRC=./nginx-runtime
ARG VUE_ON_NGINX_SRC=./vue-on-nginx
ARG WEB_SRC=./web
ARG NPM_INSTALL_ARGS=""

###################################################################################
# 1. Build web-runtime
###################################################################################
# Use the offical nginx (based on debian)
FROM nginx:stable AS runtime
ARG NGINX_RUNTIME_SRC
ARG VUE_ON_NGINX_SRC

ENV STI_SCRIPTS_PATH=/usr/libexec/s2i

# Required for HTTP Basic feature
RUN apt-get update -y && \
    apt-get install -y openssl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy our OpenShift s2i scripts over to default location
COPY ${VUE_ON_NGINX_SRC}/s2i/bin/ /usr/libexec/s2i/

# Expose this variable to OpenShift
LABEL io.openshift.s2i.scripts-url=image:///usr/libexec/s2i

# Copy config from source to container
COPY ${NGINX_RUNTIME_SRC}/nginx.conf.template /tmp/

# Copy run script
COPY ${NGINX_RUNTIME_SRC}/s2i/bin/run /usr/libexec/s2i/run

# =================================================================================
# Fix up permissions
# ref: https://torstenwalter.de/openshift/nginx/2017/08/04/nginx-on-openshift.html
# - S2I scripts must be executable
# - Make sure nginx can read and write it's working directories.
# - The container dynamically configures nginx on startup
# - The application artifacts live in /tmp
# ---------------------------------------------------------------------------------
RUN chmod -R g+rwx $STI_SCRIPTS_PATH
RUN chmod g+rw /var/cache/nginx \ 
    /var/run \
    /var/log/nginx \
    /etc/nginx/nginx.conf \
    /tmp

# =================================================================================

# Work-around for issues with S2I builds on Windows
WORKDIR /tmp

# Nginx runs on port 8080 by default
EXPOSE 8080

# Switch to usermode
USER 104

###################################################################################
# 2. Build web-artifacts
###################################################################################
FROM registry.access.redhat.com/ubi9/nodejs-${NODE_VERSION} AS artifacts
ARG NODE_VERSION
ARG WEB_SRC
ARG NPM_INSTALL_ARGS

USER root
WORKDIR /opt/app-root/src

# Copy package.json and package-lock.json
COPY ${WEB_SRC}/package*.json .

# Install packages
RUN npm ci ${NPM_INSTALL_ARGS} --ignore-scripts

# Copy the rest of the code
COPY ${WEB_SRC} .

RUN npm run build 

###################################################################################
# 3. Build web image
###################################################################################
FROM runtime
ARG VUE_ON_NGINX_SRC

# Copy the build artifacts from the 'builder' image
# to the distribution folder on the runtime image.
COPY --from=artifacts /opt/app-root/src/dist/. /tmp/app/dist/

# # Ensure S2I script copied over is runnable.
COPY ${VUE_ON_NGINX_SRC}/s2i/bin/fix-base-url /usr/libexec/s2i/fix-base-url

# Fix permissions.
USER root
RUN chmod 674 /usr/libexec/s2i/fix-base-url
RUN chmod -R 674 /tmp/app/dist/

# From nginx-runtime.
USER 104

# Since the runtime image is itself an s2i image we need to
# short circuit the s2i lifecycle.
# The runtime image "loses" its s2i runtime voodoo when it 
# is used in a dockerSrategy, which is why the explicit `CMD` is necessary
CMD  /usr/libexec/s2i/fix-base-url
