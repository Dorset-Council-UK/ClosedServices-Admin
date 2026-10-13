const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const script = fs.readFileSync(path.join(__dirname,
    "../ClosedServices-Admin.Web/wwwroot/embed-assets/service-status.js"), "utf8");

test("refresh waits one minute and reloads only the unchanged iframe URL", () => {
    const originalUrl = "https://widget.example/closed-services/embed/service-status?ServiceType=Libraries&ShowMessages=true";
    let scheduledCallback;
    let delay;
    let reloads = 0;
    const location = {
        href: originalUrl,
        reload() { reloads++; }
    };
    vm.runInNewContext(script, {
        window: {
            location,
            setTimeout(callback, milliseconds) {
                assert.equal(scheduledCallback, undefined);
                scheduledCallback = callback;
                delay = milliseconds;
            }
        }
    });
    assert.equal(delay, 60000);
    assert.equal(reloads, 0);
    scheduledCallback();
    assert.equal(reloads, 1);
    assert.equal(location.href, originalUrl);
});
