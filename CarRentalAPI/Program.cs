using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;



var builder = WebApplication.CreateBuilder(args);

// Load JWT Secret Key from Configuration
var jwtKey = builder.Configuration["JwtSettings:Secret"] ?? "super_secret_key_placeholder";

// Add services to the container.
builder.Services.AddDbContext<CarRentalContext>(options =>
    options.UseInMemoryDatabase("CarRentalDB"));
builder.Services.AddControllers();

// Configure Authentication with JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "car-rental-app",
            ValidAudience = "car-rental-users",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply Middleware
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Database Context
public class CarRentalContext : DbContext
{
    public CarRentalContext(DbContextOptions<CarRentalContext> options) : base(options) { }
    public DbSet<Car> Cars { get; set; }
    public DbSet<User> Users { get; set; }
}

// Car Model
public class Car
{
    public int Id { get; set; }
    public string Make { get; set; }
    public string Model { get; set; }
    public int Year { get; set; }
    public decimal Price { get; set; }
}

// Cars Controller
[Route("api/cars")]
[ApiController]
public class CarsController : ControllerBase
{
    private readonly CarRentalContext _context;
    public CarsController(CarRentalContext context) => _context = context;

    [HttpGet]
    public ActionResult<IEnumerable<Car>> GetCars() => _context.Cars.ToList();

    [HttpPost]
    public IActionResult AddCar(Car car)
    {
        _context.Cars.Add(car);
        _context.SaveChanges();
        return CreatedAtAction(nameof(GetCars), new { id = car.Id }, car);
    }
}

// User Model
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; } // Secure storage for passwords
}

// Authentication Controller
[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly CarRentalContext _context;
    public AuthController(CarRentalContext context) => _context = context;

    [HttpPost("login")]
    public IActionResult Login([FromBody] User user)
    {
        var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
        if (existingUser == null) return Unauthorized();

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes("super_secret_key_placeholder");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user.Username) }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(new { Token = tokenHandler.WriteToken(token) });
    }
}