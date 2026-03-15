using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.ValueObjects
{
    public class Address
    {
        public string Street { get; private set; }
        public string City { get; private set; }
        public string Country { get; private set; }
        public Address(string street, string city, string country)
        {
            Street = street;
            City = city;
            Country = country;
        }
        public Address() { }
    }
}
