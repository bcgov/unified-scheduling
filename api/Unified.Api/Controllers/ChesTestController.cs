using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Unified.Api.Models;
using Unified.Api.Validators;
using Unified.Core.Email;

namespace Unified.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dev/email")]
public sealed class ChesTestController(IEmailService emailService, TestEmailRequestValidator requestValidator)
    : ControllerBase
{
    private const string TestSubject = "Unified Scheduling CHES Test";
    private const string TestBody =
        "This is a test email sent from the Unified Scheduling local development environment.";

    [HttpPost("test")]
    [HttpLogging(HttpLoggingFields.None)]
    [ProducesResponseType(typeof(TestEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TestEmailResponse>> SendTestEmail(
        [FromBody] TestEmailRequest request,
        CancellationToken cancellationToken
    )
    {
        await requestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await emailService.SendAsync(
            new EmailMessage
            {
                To = [request.Recipient],
                Subject = TestSubject,
                Body = TestBody,
                UnifiedCorrelationId = $"local-ches-test:{Guid.NewGuid():D}",
            },
            cancellationToken
        );

        return Ok(
            new TestEmailResponse
            {
                TransactionId = result.TransactionId,
                Tag = result.Tag,
                MessageIds = result.Messages.Select(message => message.MessageId).ToArray(),
            }
        );
    }
}
