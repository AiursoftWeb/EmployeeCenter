namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class InfrastructureValueNormalizerTests
{
    [TestMethod]
    public void DomainNormalizationUsesAsciiLowercaseAndRemovesTrailingDot()
    {
        Assert.AreEqual(
            "xn--bcher-kva.example",
            InfrastructureValueNormalizer.NormalizeDomain(" BÜCHER.Example. "));
    }

    [TestMethod]
    public void DomainNormalizationRejectsEmptyLabels()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            InfrastructureValueNormalizer.NormalizeDomain("invalid..example"));
        Assert.ThrowsExactly<FormatException>(() =>
            InfrastructureValueNormalizer.NormalizeDomain("-invalid.example"));
        Assert.ThrowsExactly<FormatException>(() =>
            InfrastructureValueNormalizer.NormalizeDomain("invalid_.example"));
    }

    [TestMethod]
    public void IpNormalizationEnforcesAddressFamily()
    {
        Assert.AreEqual(
            "192.0.2.10",
            InfrastructureValueNormalizer.NormalizeOptionalIp(
                " 192.0.2.10 ",
                System.Net.Sockets.AddressFamily.InterNetwork));
        Assert.ThrowsExactly<FormatException>(() =>
            InfrastructureValueNormalizer.NormalizeOptionalIp(
                "2001:db8::1",
                System.Net.Sockets.AddressFamily.InterNetwork));
    }
}
