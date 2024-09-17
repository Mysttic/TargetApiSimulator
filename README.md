# TargetApiSimulator

## About this Project

**TargetApiSimulator** is a simple ASP.NET Core API that accepts POST requests at the `/api/target` endpoint, 
validates whether the incoming request body is valid JSON, and responds with the validation result.

This project demonstrates basic API creation and JSON validation, 
allowing users to simulate an endpoint and check if provided data follows the JSON format.

### Key Features:

- Receives JSON POST requests
- Validates JSON payloads
- Returns success (`200 OK`) or error (`400 Bad Request`) responses
- Can be used for testing or as a target API for integration scenarios

### Supported Scenarios:

- JSON validation
- Testing API endpoints
- Integration testing simulations

## How to use

To start the API, simply run the application using the provided `TargetApiSimulator.sln` solution file.

### Available Endpoints:

**POST** `/api/target`

**Description:**

Validates if the request body contains a valid JSON.

Returns:

- `200 OK` with the response `true` if the JSON is valid.
- `400 Bad Request` with an error message if the request body is not valid JSON.

Example request:

```
POST /api/target
Content-Type: application/json

{
  "key": "value"
}
```

Example response (for valid JSON):

```
200 OK
true
```

Example response (for invalid JSON):

```
400 Bad Request
{
  "ErrorMessage": "This is not JSON"
}
```

### JSON Validation Logic

The `IsValidJson` method verifies if the request body is valid JSON by attempting to parse the string. 
If the string cannot be parsed, the API will return a `400 Bad Request` status with an error message.

## Docker

You can containerize the **TargetApiSimulator** using the provided [Dockerfile](https://hub.docker.com/r/mysttic/targetapisimulator). 
Below is a sample Docker command to build and run the container.

To build the Docker image:

```bash
docker build -t targetapisimulator .
```

To run the container:

```bash
docker run -p 5000:80 targetapisimulator
```

After the container starts, you can send requests to the API at `http://localhost:5000/api/target`.

### Docker Example

Run the API inside a Docker container:

```bash
docker run -d -p 5000:80 targetapisimulator
```

You can then send POST requests to the API using tools like `curl` or Postman.

## Disclaimer

This is a simple project intended for testing and demonstration purposes. 
It is not designed for production use. 
Please review and adapt the solution to your needs.
