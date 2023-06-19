# Arguments for DEV's (comment static FROM and uncomnnet #DEV ones)
ARG UBI_VERSION=8
ARG DOTNET_VERSION=70
ARG UBI_BUILD_TAG=latest
ARG UBI_RUNTIME_TAG=latest
ARG IMAGE_BUILD=registry.access.redhat.com/ubi${UBI_VERSION}/dotnet-${DOTNET_VERSION}:${UBI_TAG}
ARG IMAGE_BASE=registry.access.redhat.com/ubi${UBI_VERSION}/dotnet-${DOTNET_VERSION}-runtime:${UBI_RUNTIME_TAG}

#DEV FROM $IMAGE_BUILD AS build
FROM registry.access.redhat.com/ubi8/dotnet-70:7.0-14 AS build

USER root
COPY . /src
WORKDIR /src

RUN dotnet publish -c Release -o /publish

RUN echo "package info:("$(dotnet list cli-proton-dotnet.sln package)")" >> /publish/VERSION.txt

#DEV FROM $IMAGE_BASE
FROM registry.access.redhat.com/ubi8/dotnet-70-runtime:7.0-14

LABEL name="Red Hat Messaging QE - Proton Dotnet CLI Image" \
      run="podman run --rm -ti <image_name:tag> /bin/bash cli-proton-dotnet-*"

USER root

# install fallocate for use by claire tests
RUN microdnf -y --setopt=install_weak_deps=0 --setopt=tsflags=nodocs install \
    util-linux \
    && dnf clean all -y

RUN mkdir /licenses
COPY ./LICENSE /licenses/LICENSE.txt
COPY ./image/ /usr/local/bin
COPY --from=build /publish/ /opt/cli-proton-dotnet

RUN chmod 0755 /usr/local/bin/cli-* && \
    chmod +x /usr/local/bin/cli-*

RUN mkdir /var/lib/cli-proton-dotnet && \
    chown -R 1001:0 /var/lib/cli-proton-dotnet  && \
    chmod -R g=u /var/lib/cli-proton-dotnet

USER 1001

VOLUME /var/lib/cli-proton-dotnet
WORKDIR /var/lib/cli-proton-dotnet

CMD ["/bin/bash"]
