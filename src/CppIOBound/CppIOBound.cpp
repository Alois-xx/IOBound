// CppIOBound.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include "pch.h"
#include <iostream>
#include <string>
#include <vector>
#include <stdio.h>
#include "CppIOBound.h"
#include <fstream>

#include <chrono>

class Stopwatch
{
public:
	Stopwatch()
	{
		_Start = std::chrono::high_resolution_clock::now();
	}

	void Start()
	{
		_Start = std::chrono::high_resolution_clock::now();
	}

	std::chrono::milliseconds Stop()
	{
		_Stop = std::chrono::high_resolution_clock::now();
		return std::chrono::duration_cast<std::chrono::milliseconds>(_Stop - _Start);
	}
private:
	std::chrono::high_resolution_clock::time_point _Start;
	std::chrono::high_resolution_clock::time_point _Stop;
};


void ParseLine(std::vector<double> &doubles, std::vector<int> &ints, char *pline)
{
	char *pDouble = strtok(pline, " \n");

	if (pDouble != nullptr)
	{
		double d = atof(pDouble);
		char *pInt = strtok(NULL, " \n");
		if (pInt != nullptr && pInt != pDouble)
		{
			int i = atoi(pInt);
			doubles.push_back(d);
			ints.push_back(i);
		}
	}
	
}

std::ifstream::pos_type filesize(const char* filename)
{
	std::ifstream in(filename, std::ifstream::ate | std::ifstream::binary);
	return in.tellg();
}


void ReadFileFgets(const char *fileName)
{
	FILE *fp = fopen(fileName, "r");
	if (fp == nullptr)
	{
		printf("\nCould not open file %s", fileName);
		return;
	}

	Stopwatch sw;

	char pLine[1024];
	std::vector<double> doubles;
	std::vector<int> ints;
	int lines = 0;

	while (fgets(pLine, sizeof(pLine), fp) != nullptr)
	{
		lines++;
		ParseLine(doubles, ints, pLine);
	}
	auto ms = sw.Stop();
	long long MB = filesize(fileName) / (1024 * 1024L);
	printf("C++ fgets               %lld MB in %.2fs, %.2f MB/s (%d lines)\n", MB, ms.count() / 1000.0f, (float)MB / (ms.count() / 1000L), lines);

}

int main(int argc, const char **argv)
{
	if (argc == 2)
	{
		ReadFileFgets(argv[1]);
	}
	else
	{
		printf("\nPlease supply path to input file NumericData.txt which will be created once you have run IOBound.exe in the net472 folder.");
	}


}