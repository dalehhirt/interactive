// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { expect } from "chai";
import * as interactive from "../src/dotnet-interactive"
import { createDotnetInteractiveClient } from "../src/dotnet-interactive/KernelClientImpl";
import * as fetchMock from "fetch-mock";
import { asKernelClientContainer } from "./testSupprot";

describe("dotnet-interactive", () => {

    afterEach(fetchMock.restore);
    describe("initialisation", () => {
        it("injects function to create scope for dotnet interactive", () => {
            let global: any = {};
            interactive.init(global);

            expect(typeof (global.getDotnetInteractiveScope))
                .to
                .equal('function');
        });

        it("injects function to create dotnet interactive client", () => {
            let global: any = {};
            interactive.init(global);

            expect(typeof (global.createDotnetInteractiveClient))
                .to
                .equal('function');
        });
    });

    describe("kernel discovery", () => {
        it("creates kernel clients for all discovered kernesl", async () => {
            const rootUrl = "https://dotnet.interactive.com:999";
            const expectedKernels = require("./Responses/kernlesResponse.json");
            fetchMock.get(`${rootUrl}/kernels`, expectedKernels);
            let client = asKernelClientContainer(await createDotnetInteractiveClient(rootUrl));

            for (let kernelName of expectedKernels) {
                expect(client[kernelName]).not.to.be.undefined;
            }

        })
    });

    describe("scopes", () => {
        it("can be retrieved", () => {
            let global: any = {};
            interactive.init(global);

            let scope = global.getDotnetInteractiveScope("scopeid");
            expect(scope).not.to.be.null;
            expect(scope).not.to.be.undefined;

        })
    });
});