using System;

namespace Shared.Security
{
    public interface ICrypter : IDisposable
    {
        void EncodeBuffer(ref byte[] buffer);
        void DecodeBuffer(ref byte[] buffer);
       
    }
}
