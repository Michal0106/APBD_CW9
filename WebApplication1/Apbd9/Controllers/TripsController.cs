using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Context;
using WebApplication1.Models;
using WebApplication1.Models.Dto;

namespace WebApplication1.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TripsController : ControllerBase
{
    private readonly Apbd9Context _apbd9Context;

    public TripsController(Apbd9Context apbd9Context)
    {
        _apbd9Context = apbd9Context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrips(int page = 1, int pageSize = 10)
    {
        var tripsQuery = _apbd9Context.Trips
            .OrderByDescending(t => t.DateFrom)
            .Select(t => new 
            {
                t.Name,
                t.Description,
                t.DateFrom,
                t.DateTo,
                t.MaxPeople,
                Countries = t.IdCountries.Select(c => new { c.Name }).ToList(),
                Clients = t.ClientTrips.Select(ct => new { ct.IdClientNavigation.FirstName, ct.IdClientNavigation.LastName }).ToList()
            });

        var totalTrips = await tripsQuery.CountAsync();
        var trips = await tripsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new
        {
            pageNum = page,
            pageSize,
            allPages = totalTrips,
            trips
        };

        return Ok(response);
    }
    
    [HttpDelete("{idClient}")]
    public async Task<IActionResult> DeleteClient(int idClient)
    {
        var client = await _apbd9Context.Clients.Include(c => c.ClientTrips).FirstOrDefaultAsync(c => c.IdClient == idClient);

        if (client == null)
        {
            return NotFound(new { message = "Client not found" });
        }

        if (client.ClientTrips.Any())
        {
            return BadRequest(new { message = "Client has assigned trips and cannot be deleted" });
        }

        _apbd9Context.Clients.Remove(client);
        await _apbd9Context.SaveChangesAsync();

        return Ok(new { message = "Client deleted successfully" });
    }
    
    [HttpPost("{idTrip}/clients")]
    public async Task<IActionResult> AssignClientToTrip(int idTrip, [FromBody] ClientDto clientDto)
    {
        var trip = await _apbd9Context.Trips.FirstOrDefaultAsync(t => t.IdTrip == idTrip);
        if (trip == null)
        {
            return NotFound(new { message = "Trip not found" });
        }

        if (trip.DateFrom <= DateTime.Now)
        {
            return BadRequest(new { message = "Cannot register for a trip that has already started or ended" });
        }

        var existingClient = await _apbd9Context.Clients.FirstOrDefaultAsync(c => c.Pesel == clientDto.Pesel);
        if (existingClient != null)
        {
            var clientTrip = await _apbd9Context.ClientTrips.FirstOrDefaultAsync(ct => ct.IdClient == existingClient.IdClient && ct.IdTrip == idTrip);
            if (clientTrip != null)
            {
                return BadRequest(new { message = "Client is already registered for this trip" });
            }
        }

        var client = existingClient ?? new Client
        {
            FirstName = clientDto.FirstName,
            LastName = clientDto.LastName,
            Email = clientDto.Email,
            Telephone = clientDto.Telephone,
            Pesel = clientDto.Pesel
        };

        var newClientTrip = new ClientTrip
        {
            IdClient = client.IdClient,
            IdTrip = idTrip,
            RegisteredAt = DateTime.Now,
            PaymentDate = clientDto.PaymentDate
        };

        _apbd9Context.ClientTrips.Add(newClientTrip);

        if (existingClient == null)
        {
            _apbd9Context.Clients.Add(client);
        }

        await _apbd9Context.SaveChangesAsync();

        return Ok(new { message = "Client successfully registered for the trip" });
    }
}