﻿namespace PasswordManager.Converters
{
    public sealed class BooleanTFConverter : BooleanConverter<bool>
    {
        public BooleanTFConverter() : base(true, false) { }
    }
}
