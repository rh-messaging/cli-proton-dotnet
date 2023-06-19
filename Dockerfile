# Arguments for DEV's (comment static FROM and uncomnnet #DEV ones)
ARG UBI_VERSION=8
ARG DOTNET_VERSION=70
ARG UBI_BUILD_TAG=latest
ARG UBI_RUNTIME_TAG=latest
ARG IMAGE_BUILD=registry.access.redhat.com/ubi${UBI_VERSION}/dotnet-${DOTNET_VERSION}:${UBI_TAG}
ARG IMAGE_BASE=registry.access.redhat.com/ubi${UBI_VERSION}/dotnet-${DOTNET_VERSION}-runtime:${UBI_RUNTIME_TAG}

#DEV FROM $IMAGE_BUILD AS build
FROM --platform=$BUILDPLATFORM registry.access.redhat.com/ubi8/dotnet-70@sha256:de739e02366216892e0e12b391acaab39542c65902e89a53a119eff7d21ca3c5 AS build

USER root
COPY . /src
WORKDIR /src

# https://community.ibm.com/community/user/powerdeveloper/blogs/alhad-deshpande/2023/01/13/identityserver-sqlite-db-on-net-7
RUN microdnf install -y findutils sed
RUN find -name '*.csproj' -exec sed -i 's|<TargetFramework>net6.0</TargetFramework>|<TargetFramework>net7.0</TargetFramework>|' {} \;

RUN dotnet build -c Release
RUN dotnet publish --no-build --self-contained false -c Release -o /publish

RUN echo "package info:("$(dotnet list cli-proton-dotnet.sln package)")" >> /publish/VERSION.txt

#DEV FROM $IMAGE_BASE
FROM --platform=$TARGETPLATFORM registry.access.redhat.com/ubi8/dotnet-70-runtime:7.0-14

LABEL name="Red Hat Messaging QE - Proton Dotnet CLI Image" \
      run="podman run --rm -ti <image_name:tag> /bin/bash cli-proton-dotnet-*"

USER root

# install fallocate for use by claire tests
RUN microdnf -y --setopt=install_weak_deps=0 --setopt=tsflags=nodocs install \
    util-linux \
    && microdnf clean all -y

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
