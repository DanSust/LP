using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LP.Common;

public class JwtTokenParser
{
    private readonly IConfiguration _cfg;
    public JwtTokenParser(IConfiguration cfg) => _cfg = cfg;

    public (bool ok, Claims? claims) Parse(string idToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);

            // быстрая проверка без сети: audience + issuer + не протух
            var valid = jwt.Issuer == "https://accounts.google.com" &&
                        jwt.Audiences.Contains(_cfg["OAuth:Google:ClientId"]) &&
                        jwt.ValidTo > DateTime.UtcNow;

            if (!valid) return (false, null);

            return (true, new Claims(
                jwt.Subject,
                jwt.Claims.First(c => c.Type == "ProviderId").Value,
                jwt.Claims.First(c => c.Type == "name").Value,
                jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value));
        }
        catch { return (false, null); }
    }

    public string JwtGenerate(string Id, string ProviderId, string Name)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_cfg["Jwt:Secret"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Id),
            new Claim("ProviderId", ProviderId),
            new Claim("name", Name)
        };

        var token = new JwtSecurityToken(
            //issuer: _cfg["Jwt:Issuer"],
            //audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddYears(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}



public record Claims(string Sub, string Email, string Name, string? Picture);