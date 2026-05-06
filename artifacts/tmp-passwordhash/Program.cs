using Microsoft.AspNetCore.Identity;

var hasher = new PasswordHasher<object>();
Console.WriteLine(hasher.HashPassword(new object(), "q"));
