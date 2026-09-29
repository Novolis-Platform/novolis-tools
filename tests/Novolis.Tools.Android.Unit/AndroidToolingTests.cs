using Novolis.IO.Mobile.Android;

namespace Novolis.Tools.Android.Unit;

public sealed class AndroidToolingTests
{
    [Test]
    public async Task SelectorRequiresSerialWhenSeveralPhonesAreReady()
    {
        var result = AndroidDeviceSelector.Resolve(
        [
            new AdbDevice("phone-a", AdbDeviceState.Device),
            new AdbDevice("phone-b", AdbDeviceState.Device),
        ]);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Failure!.Kind).IsEqualTo(AndroidFailureKind.AmbiguousDevice);
    }

    [Test]
    public async Task RedactionKeepsOrdinaryTextAndHidesTokens()
    {
        var redacted = AndroidOutputRedactor.Redact(
            "Authorization: Bearer abc123; ordinary=value; client_secret=hidden");

        await Assert.That(redacted).DoesNotContain("abc123");
        await Assert.That(redacted).DoesNotContain("hidden");
        await Assert.That(redacted).Contains("ordinary=value");
    }
}
