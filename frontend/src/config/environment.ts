// The platform's environment module, same path and shape, so copied files import it unchanged.
const environment = {
  API_BASE_URL: ((import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:9950').replace(/\/+$/, ''),
  IS_PLATFORM: import.meta.env.VITE_IS_PLATFORM !== 'false',
  LICENSE_ENABLED: import.meta.env.VITE_LICENSE_ENABLED === 'true',
};

export default environment;
