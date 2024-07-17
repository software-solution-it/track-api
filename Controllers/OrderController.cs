using Microsoft.AspNetCore.Mvc;
using track_api.Models;
using track_api.Services;

namespace track_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobController : ControllerBase
    {
        private readonly GetTrackService _getTrackService;

        public JobController(GetTrackService getTrackService)
        {
            _getTrackService = getTrackService;
        }


        [HttpGet("{orderId}")]
        public IActionResult GetJobByOrderId(string orderId)
        {
            string numericOrderId = System.Text.RegularExpressions.Regex.Replace(orderId, @"\D", "");
            if (int.TryParse(numericOrderId, out int numericId))
            {
                var job = _getTrackService.GetJobByOrderId(numericId);
            if (job == null)
            {
                return NotFound($"Job with OrderId: {orderId} not found.");
            }
            return Ok(job);
            }
            return BadRequest("Invalid Order ID format.");
        }

        [HttpGet("tracking/{orderId}")]
        public IActionResult GetTrackingCodeByOrderId(string orderId)
        {
            string numericOrderId = System.Text.RegularExpressions.Regex.Replace(orderId, @"\D", "");
            if (int.TryParse(numericOrderId, out int numericId))
            {
                var job = _getTrackService.GetTrackingCode(numericId);
                if (job == null)
                {
                    return NotFound($"Job with OrderId: {orderId} not found.");
                }
                return Ok(job);
            }
            return BadRequest("Invalid Order ID format.");
        }


    }
}
