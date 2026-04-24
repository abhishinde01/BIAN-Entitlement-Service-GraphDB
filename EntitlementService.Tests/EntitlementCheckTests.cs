using EntitlementService.Controllers;
using EntitlementService.Graph;
using EntitlementService.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EntitlementService.Tests;

public class EntitlementCheckTests
{
    private static EntitlementController BuildController(Mock<IGraphService> mock) => new(mock.Object);

    private static (bool IsAllowed, string Reason) ExtractResponse(ActionResult<EntitlementCheckResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EntitlementCheckResponse>(ok.Value);
        return (response.IsAllowed, response.Reason);
    }

    [Fact]
    public async Task Test_Allow_WhenValidSubjectHasPermission()
    {
        var mock = new Mock<IGraphService>();
        mock.Setup(s => s.CheckEntitlementAsync("CUSTOMER-001", "execute-trade", "RESOURCE-PORTFOLIO-001"))
            .ReturnsAsync(new EntitlementCheckResponse(true, "Permission granted via ROLE-TRADER", "execute-trade"));

        var (isAllowed, reason) = ExtractResponse(
            await BuildController(mock).CheckEntitlement(
                new EntitlementCheckRequest("CUSTOMER-001", "execute-trade", "RESOURCE-PORTFOLIO-001")));

        Assert.True(isAllowed);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public async Task Test_Deny_WhenSubjectHasDenyEntitlement()
    {
        var mock = new Mock<IGraphService>();
        mock.Setup(s => s.CheckEntitlementAsync("CUSTOMER-003", "execute-trade", "RESOURCE-PORTFOLIO-001"))
            .ReturnsAsync(new EntitlementCheckResponse(false, "Explicitly denied", null));

        var (isAllowed, reason) = ExtractResponse(
            await BuildController(mock).CheckEntitlement(
                new EntitlementCheckRequest("CUSTOMER-003", "execute-trade", "RESOURCE-PORTFOLIO-001")));

        Assert.False(isAllowed);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public async Task Test_Deny_WhenSubjectHasNoEntitlement()
    {
        var mock = new Mock<IGraphService>();
        mock.Setup(s => s.CheckEntitlementAsync("CUSTOMER-999", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new EntitlementCheckResponse(false, "No entitlement found", null));

        var (isAllowed, reason) = ExtractResponse(
            await BuildController(mock).CheckEntitlement(
                new EntitlementCheckRequest("CUSTOMER-999", "view-account", "RESOURCE-PORTFOLIO-001")));

        Assert.False(isAllowed);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public async Task Test_Deny_WhenWrongResource()
    {
        var mock = new Mock<IGraphService>();
        mock.Setup(s => s.CheckEntitlementAsync("CUSTOMER-002", "execute-trade", It.IsAny<string>()))
            .ReturnsAsync(new EntitlementCheckResponse(false, "No entitlement found for this resource", null));

        var (isAllowed, reason) = ExtractResponse(
            await BuildController(mock).CheckEntitlement(
                new EntitlementCheckRequest("CUSTOMER-002", "execute-trade", "RESOURCE-PORTFOLIO-999")));

        Assert.False(isAllowed);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public async Task Test_Allow_AdminCanManageUsers()
    {
        var mock = new Mock<IGraphService>();
        mock.Setup(s => s.CheckEntitlementAsync("CUSTOMER-003", "manage-users", "RESOURCE-SYSTEM"))
            .ReturnsAsync(new EntitlementCheckResponse(true, "Permission granted via ROLE-ADMIN", "manage-users"));

        var (isAllowed, reason) = ExtractResponse(
            await BuildController(mock).CheckEntitlement(
                new EntitlementCheckRequest("CUSTOMER-003", "manage-users", "RESOURCE-SYSTEM")));

        Assert.True(isAllowed);
        Assert.False(string.IsNullOrEmpty(reason));
    }
}
