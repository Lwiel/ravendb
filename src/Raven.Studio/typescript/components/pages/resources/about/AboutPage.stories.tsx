import {
    licenseArgType,
    securityClearanceArgType,
    supportStatusArgType,
    withBootstrap5,
    withStorybookContexts,
} from "test/storybookTestUtils";
import { Meta, StoryObj } from "@storybook/react";
import { AboutPage as AboutPageComponent } from "./AboutPage";
import React from "react";
import { mockServices } from "test/mocks/services/MockServices";
import { mockStore } from "test/mocks/store/MockStore";

export default {
    title: "Pages/About page",
    decorators: [withStorybookContexts, withBootstrap5],
    argTypes: {
        securityClearance: securityClearanceArgType,
        licenseType: licenseArgType,
        supportStatus: supportStatusArgType,
    },
} satisfies Meta<AboutPageStoryProps>;

interface AboutPageStoryProps {
    licenseType: Raven.Server.Commercial.LicenseType;
    securityClearance: Raven.Client.ServerWide.Operations.Certificates.SecurityClearance;
    licenseServerConnection: boolean;
    passiveServer: boolean;
    isIsv: boolean;
    cloud: boolean;
    supportStatus: Raven.Server.Commercial.Status;
}

function commonInit(props: AboutPageStoryProps) {
    const { licenseService } = mockServices;
    const { license, accessManager, cluster } = mockStore;

    accessManager.with_securityClearance(props.securityClearance);
    cluster.with_ClientVersion();
    cluster.with_ServerVersion();
    cluster.with_PassiveServer(props.passiveServer);
    license.with_License({
        Type: props.licenseType,
        IsIsv: props.isIsv,
        IsCloud: props.cloud,
    });
    license.with_Support({
        Status: props.supportStatus,
    });

    if (props.licenseServerConnection) {
        licenseService.withConnectivityCheck();
    } else {
        licenseService.withConnectivityCheck({
            connected: false,
            exception: "Can't connect to api.ravendb.net",
        });
    }

    licenseService.withLatestVersion();
    licenseService.withGetConfigurationSettings();
}

const defaultArgs: AboutPageStoryProps = {
    licenseType: "Enterprise",
    isIsv: false,
    cloud: false,
    passiveServer: false,
    supportStatus: "NoSupport",
    licenseServerConnection: true,
    securityClearance: "ClusterAdmin",
};

const render = (props: AboutPageStoryProps) => {
    commonInit(props);

    return <AboutPageComponent />;
};

export const AboutPage: StoryObj<AboutPageStoryProps> = {
    render,
    args: defaultArgs,
};

export const ConnectionFailure: StoryObj<AboutPageStoryProps> = {
    render,
    args: {
        ...defaultArgs,
        licenseServerConnection: false,
    },
};
