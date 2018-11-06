What is it?
===========

IOBound reads a file with 10 million doubles and integers with different approaches to test
 how fast you can get with .NET Core and the full .NET Framework. 
It is a self contained executable which creates the test data automatically if it does not yet exist
in the current directory. 


## Usage ##

The .NET 4.7.2 application is executed with

> IOBound.exe

![Image](https://github.com/Alois-xx/IOBound/images/IOBound_Net742.png)

The .NET Core application as usual with 

> dotnet IOBound.dll

![Image](https://github.com/Alois-xx/IOBound/images/IOBound_NetCore2.1.png)

## Why? ##

I wanted to check how much faster the `Span<T>` based Apis of .NET Core perform compared 
to the regular .NET Framework. The C# 7.2 compiler supports Span natively but to unlock the full
performance you need support of the JIT compiler to generate truly efficient code. 
This Span supporting JIT compiler will not be ported back to the regular .NET Framework due to
application compatibility concerns (at least that is the story up to now Oct. 2018). 
Now you can check for yourself if you really need to switch over to .NET Core to squeeze out 
the last bit of performance or not.
