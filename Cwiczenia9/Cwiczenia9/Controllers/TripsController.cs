using Cwiczenia9.Data;
using Cwiczenia9.DTOs;
using Cwiczenia9.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cwiczenia9.Controllers;

[ApiController]
public class TripsController : ControllerBase
{
    private readonly FrameworkContext _context;
    public TripsController(FrameworkContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("api/trips")]
    public async Task<ActionResult> GetTrips(int page = 1, int pageSize = 10)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 10;
        }

        var ileTripow = await _context.Trips.CountAsync();
        
        var trips = await _context.Trips
            .Include(trip => trip.IdCountries)
            .Include(trip => trip.ClientTrips).ThenInclude(client_trip => client_trip.IdClientNavigation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var outpot = new
        {
            PageNum = page,
            PageSize = pageSize,
            AllPages = Math.Ceiling(ileTripow / (double)pageSize),
            Trips = trips.Select(trip => new
            {
                trip.Name,
                trip.Description,
                trip.DateFrom,
                trip.DateTo,
                trip.MaxPeople,
                Countries = trip.IdCountries.Select(c => new { c.Name }),
                Clients = trip.ClientTrips.Select(ct => new 
                {
                    ct.IdClientNavigation.FirstName,
                    ct.IdClientNavigation.LastName
                })
            }).OrderByDescending(t => t.DateFrom)
        };

        return Ok(outpot);
    }

    [HttpDelete]
    [Route("api/clients/{idClient:int}")]
    public async Task<ActionResult> DeleteClient(int idClient)
    {
        var client = await _context.Clients.FindAsync(idClient);
        if (client is null)
        {
            return NotFound($"Client with id: {idClient} doesn't exist");
        }
        
        var existsTrip = await _context.ClientTrips.AnyAsync(x => x.IdClient == idClient);
        if (existsTrip)
        {
            return NotFound($"Client with id: {idClient} have trips");
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        return Ok($"Client with {idClient} have been deleted");
    }
    
    [HttpPost]
    [Route("api/trips/{idTrip}/clients")]
    public async Task<ActionResult> PostClient(ClientDTO client)
    {
        var exist = await _context.Clients.AnyAsync(x => x.Pesel == client.Pesel);
        if (exist)
        {
            return NotFound($"Client with pesel: {client.Pesel} exist");
        }

        exist = await _context.ClientTrips.AnyAsync(x =>
            x.IdClientNavigation.Pesel == client.Pesel && x.IdTrip == client.IdTrip);
        if (exist)
        {
            return NotFound($"Client with pesel: {client.Pesel} jest juz na wycieczce");
        }

        exist = await _context.Trips.AnyAsync(x =>
            x.IdTrip == client.IdTrip && x.DateFrom > DateTime.Now);
        if (exist)
        {
            return NotFound("Nie mozna zapisac sie na wycieche");
        }

        Client c = new Client()
        {
            FirstName = client.FirstName,
            LastName = client.LastName,
            Email = client.Email,
            Telephone = client.Telephone,
            Pesel = client.Pesel
        };
        
        _context.Add(c);
        var tip = await _context.Trips.FindAsync(client.IdTrip);

        _context.Add(new ClientTrip()
        {
            IdTrip = client.IdTrip,
            IdClientNavigation = c,
            IdTripNavigation = tip,
            PaymentDate = client.PaymentDate,
            RegisteredAt = DateTime.Now
        });
        
        await _context.SaveChangesAsync();

        return Ok($"Client have been added");
    }
}