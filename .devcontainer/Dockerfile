FROM mcr.microsoft.com/devcontainers/base:ubuntu

# Install dependencies
RUN apt-get update && \
    apt-get install -y \
    curl \
    tar \
    wget \
    postgresql-client && \
    apt-get clean

# Install oc cli
RUN curl -s -L https://downloads-openshift-console.apps.silver.devops.gov.bc.ca/amd64/linux/oc.tar | \
    tar -xv -C /usr/local/bin

# ==========================================================================================================================
# Install S2I
# --------------------------------------------------------------------------------------------------------------------------
ENV S2I_VERSION=source-to-image-v1.4.0-d3544c7e-linux-amd64.tar.gz
RUN wget https://github.com/openshift/source-to-image/releases/download/v1.4.0/${S2I_VERSION} -O /tmp/${S2I_VERSION} && \
    tar -xzf /tmp/${S2I_VERSION} -C /usr/local/bin && \
    rm /tmp/${S2I_VERSION}

# Verify installation
RUN s2i version
# ==========================================================================================================================

# Set the default command
CMD ["bash"]
